using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Topomatic.ToolBridge.Services
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public sealed class ObjectStorage
    {
        private const int PruneIntervalOperations = 50;

        private readonly object m_SyncRoot;
        private readonly Dictionary<Guid, WeakReference<object>> m_Objects;
        private ConditionalWeakTable<object, GuidBox> m_Guids;
        private int m_OperationsSinceLastPrune;

        public ObjectStorage()
        {
            m_SyncRoot = new object();
            m_Objects = new Dictionary<Guid, WeakReference<object>>();
            m_Guids = new ConditionalWeakTable<object, GuidBox>();
        }

        public Guid AddObject(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                if (m_Guids.TryGetValue(obj, out _))
                    throw new InvalidOperationException("[ObjectStorage] The object is already stored.");
                var guid = Guid.NewGuid();
                AddObjectCore(guid, obj);
                return guid;
            }
        }

        public void AddObject(Guid guid, object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                if (TryGetObjectCore(guid, out _))
                    throw new InvalidOperationException($"[ObjectStorage] An object with GUID '{guid}' is already stored.");
                if (m_Guids.TryGetValue(obj, out _))
                    throw new InvalidOperationException("[ObjectStorage] The object is already stored.");
                AddObjectCore(guid, obj);
            }
        }

        public object GetObject(Guid guid)
        {
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                if (TryGetObjectCore(guid, out var obj))
                    return obj;
                throw new InvalidOperationException($"[ObjectStorage] No live object with GUID '{guid}' is stored.");
            }
        }

        public Guid GetGuid(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                if (m_Guids.TryGetValue(obj, out var guid))
                    return guid.Value;
                throw new InvalidOperationException("[ObjectStorage] The specified object is not stored.");
            }
        }

        public bool HasObject(Guid guid)
        {
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                return TryGetObjectCore(guid, out _);
            }
        }

        public bool HasObject(object obj)
        {
            if (obj == null)
                return false;
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                return m_Guids.TryGetValue(obj, out _);
            }
        }

        public bool RemoveObject(Guid guid)
        {
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                if (!m_Objects.TryGetValue(guid, out var reference))
                    return false;
                m_Objects.Remove(guid);
                if (reference.TryGetTarget(out var obj))
                    m_Guids.Remove(obj);
                return true;
            }
        }

        public bool RemoveObject(object obj)
        {
            if (obj == null)
                return false;
            lock (m_SyncRoot)
            {
                PruneIfNeeded();
                if (!m_Guids.TryGetValue(obj, out var guid))
                    return false;
                m_Guids.Remove(obj);
                m_Objects.Remove(guid.Value);
                return true;
            }
        }

        public void Clear()
        {
            lock (m_SyncRoot)
            {
                m_Objects.Clear();
                m_Guids = new ConditionalWeakTable<object, GuidBox>();
                m_OperationsSinceLastPrune = 0;
            }
        }

        private void AddObjectCore(Guid guid, object obj)
        {
            m_Objects.Add(guid, new WeakReference<object>(obj));
            m_Guids.Add(obj, new GuidBox(guid));
        }

        private bool TryGetObjectCore(Guid guid, out object obj)
        {
            obj = null;
            if (!m_Objects.TryGetValue(guid, out var reference))
                return false;
            if (reference.TryGetTarget(out obj))
                return true;
            m_Objects.Remove(guid);
            return false;
        }

        private void PruneIfNeeded()
        {
            m_OperationsSinceLastPrune++;
            if (m_OperationsSinceLastPrune < PruneIntervalOperations)
                return;
            m_OperationsSinceLastPrune = 0;
            var deadGuids = new List<Guid>();
            foreach (var pair in m_Objects)
            {
                if (!pair.Value.TryGetTarget(out _))
                    deadGuids.Add(pair.Key);
            }
            foreach (var guid in deadGuids)
                m_Objects.Remove(guid);
        }

        private sealed class GuidBox
        {
            public GuidBox(Guid value)
            {
                Value = value;
            }

            public Guid Value { get; }
        }
    }
}
