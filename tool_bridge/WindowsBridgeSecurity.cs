using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace Topomatic.ToolBridge
{
    internal sealed class WindowsBridgeSecurityContext : IDisposable
    {
        private const string MutexNamePrefix = @"Local\Topomatic.Robur.ToolBridge.";
        private readonly SecurityIdentifier m_LogonSid;
        private readonly SecurityIdentifier m_UserSid;
        private Mutex m_Mutex;

        private WindowsBridgeSecurityContext(SecurityIdentifier logonSid, SecurityIdentifier userSid, Mutex mutex)
        {
            m_LogonSid = logonSid;
            m_UserSid = userSid;
            m_Mutex = mutex;
        }

        internal static WindowsBridgeSecurityContext Acquire()
        {
            using (var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query))
            {
                var logonSid = GetLogonSid(identity);
                var userSid = identity.User
                    ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
                var security = CreateMutexSecurity(logonSid, userSid);
                var mutexName = MutexNamePrefix + HashSid(logonSid.Value);

                bool createdNew;
                Mutex mutex;
                try
                {
                    mutex = new Mutex(false, mutexName, out createdNew, security);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException("Another tool bridge instance has reserved the session mutex.", ex);
                }

                if (!createdNew)
                {
                    mutex.Dispose();
                    throw new InvalidOperationException("Another tool bridge instance is already running in this Windows logon session.");
                }

                return new WindowsBridgeSecurityContext(logonSid, userSid, mutex);
            }
        }

        internal NamedPipeServerStream CreatePipe(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
                throw new ArgumentException("Pipe name must not be empty.", nameof(pipeName));

            var pipeSecurity = CreatePipeSecurity(m_LogonSid, m_UserSid);
            var descriptor = pipeSecurity.GetSecurityDescriptorBinaryForm();
            var descriptorHandle = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
            try
            {
                var attributes = new NativeMethods.SecurityAttributes
                {
                    Length = Marshal.SizeOf(typeof(NativeMethods.SecurityAttributes)),
                    SecurityDescriptor = descriptorHandle.AddrOfPinnedObject(),
                    InheritHandle = false
                };
                var fullPipeName = @"\\.\pipe\" + pipeName;
                var handle = NativeMethods.CreateNamedPipe(
                    fullPipeName,
                    NativeMethods.PipeAccessDuplex
                        | NativeMethods.FileFlagOverlapped
                        | NativeMethods.FileFlagFirstPipeInstance,
                    NativeMethods.PipeTypeByte
                        | NativeMethods.PipeReadModeByte
                        | NativeMethods.PipeWait
                        | NativeMethods.PipeRejectRemoteClients,
                    1,
                    4096,
                    4096,
                    0,
                    ref attributes);

                if (handle == null || handle.IsInvalid)
                {
                    var error = Marshal.GetLastWin32Error();
                    handle?.Dispose();
                    throw new Win32Exception(error, "Could not create the secured tool bridge pipe.");
                }

                try
                {
                    return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle);
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }
            }
            finally
            {
                descriptorHandle.Free();
            }
        }

        internal bool IsCurrentLogonClient(NamedPipeServerStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            SecurityIdentifier clientLogonSid = null;
            stream.RunAsClient(() =>
            {
                using (var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query))
                    clientLogonSid = GetLogonSid(identity);
            });
            return m_LogonSid.Equals(clientLogonSid);
        }

        public void Dispose()
        {
            var mutex = m_Mutex;
            m_Mutex = null;
            mutex?.Dispose();
        }

        private static MutexSecurity CreateMutexSecurity(SecurityIdentifier logonSid, SecurityIdentifier userSid)
        {
            var security = new MutexSecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(userSid);
            security.AddAccessRule(
                new MutexAccessRule(
                    logonSid,
                    MutexRights.Modify | MutexRights.Synchronize,
                    AccessControlType.Allow
                )
            );
            return security;
        }

        private static PipeSecurity CreatePipeSecurity(SecurityIdentifier logonSid, SecurityIdentifier userSid)
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.SetOwner(userSid);
            security.AddAccessRule(
                new PipeAccessRule(
                    logonSid,
                    PipeAccessRights.ReadData
                        | PipeAccessRights.WriteData
                        | PipeAccessRights.ReadAttributes
                        | PipeAccessRights.WriteAttributes
                        | PipeAccessRights.Synchronize,
                    AccessControlType.Allow
                )
            );
            return security;
        }

        private static SecurityIdentifier GetLogonSid(WindowsIdentity identity)
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            var requiredLength = 0;
            NativeMethods.GetTokenInformation(
                identity.Token,
                NativeMethods.TokenInformationClass.TokenGroups,
                IntPtr.Zero,
                0,
                out requiredLength);
            var error = Marshal.GetLastWin32Error();
            if (requiredLength <= 0 || error != NativeMethods.ErrorInsufficientBuffer)
                throw new Win32Exception(error, "Could not determine the Windows token groups buffer size.");

            var buffer = Marshal.AllocHGlobal(requiredLength);
            try
            {
                if (!NativeMethods.GetTokenInformation(
                    identity.Token,
                    NativeMethods.TokenInformationClass.TokenGroups,
                    buffer,
                    requiredLength,
                    out requiredLength))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the Windows token groups.");
                }

                var groupCount = Marshal.ReadInt32(buffer);
                var groupOffset = Marshal.OffsetOf(
                    typeof(NativeMethods.TokenGroups),
                    nameof(NativeMethods.TokenGroups.FirstGroup)).ToInt32();
                var groupSize = Marshal.SizeOf(typeof(NativeMethods.SidAndAttributes));
                for (var index = 0; index < groupCount; index++)
                {
                    var groupPointer = IntPtr.Add(buffer, groupOffset + index * groupSize);
                    var group = (NativeMethods.SidAndAttributes)Marshal.PtrToStructure(
                        groupPointer,
                        typeof(NativeMethods.SidAndAttributes));
                    if ((group.Attributes & NativeMethods.SeGroupLogonId) == NativeMethods.SeGroupLogonId)
                        return new SecurityIdentifier(group.Sid);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            throw new InvalidOperationException("The Windows logon SID is unavailable.");
        }

        private static string HashSid(string sid)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sid));
                var builder = new StringBuilder(32);
                for (var index = 0; index < 16; index++)
                    builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }

        private static class NativeMethods
        {
            internal const uint PipeAccessDuplex = 0x00000003;
            internal const uint FileFlagFirstPipeInstance = 0x00080000;
            internal const uint FileFlagOverlapped = 0x40000000;
            internal const uint PipeTypeByte = 0x00000000;
            internal const uint PipeReadModeByte = 0x00000000;
            internal const uint PipeWait = 0x00000000;
            internal const uint PipeRejectRemoteClients = 0x00000008;
            internal const int ErrorInsufficientBuffer = 122;
            internal const uint SeGroupLogonId = 0xC0000000;

            internal enum TokenInformationClass
            {
                TokenGroups = 2
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct SecurityAttributes
            {
                internal int Length;
                internal IntPtr SecurityDescriptor;
                [MarshalAs(UnmanagedType.Bool)]
                internal bool InheritHandle;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct SidAndAttributes
            {
                internal IntPtr Sid;
                internal uint Attributes;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct TokenGroups
            {
                internal uint GroupCount;
                internal SidAndAttributes FirstGroup;
            }

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetTokenInformation(
                IntPtr tokenHandle,
                TokenInformationClass tokenInformationClass,
                IntPtr tokenInformation,
                int tokenInformationLength,
                out int returnLength);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern SafePipeHandle CreateNamedPipe(
                string name,
                uint openMode,
                uint pipeMode,
                uint maxInstances,
                uint outBufferSize,
                uint inBufferSize,
                uint defaultTimeout,
                ref SecurityAttributes securityAttributes);
        }
    }
}
