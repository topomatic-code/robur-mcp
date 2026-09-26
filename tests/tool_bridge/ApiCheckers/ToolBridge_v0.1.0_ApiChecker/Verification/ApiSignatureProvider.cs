using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.Immutable;

public sealed class ApiSignatureProvider : ISignatureTypeProvider<string, object>
{
    public string GetArrayType(string element, ArrayShape shape) => element + "[" + new string(',', shape.Rank - 1) + "]";
    public string GetByReferenceType(string element) => element + "&";
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetGenericInstantiation(string type, ImmutableArray<string> arguments) => type + "<" + string.Join(",", arguments) + ">";
    public string GetGenericMethodParameter(object context, int index) => "!!" + index;
    public string GetGenericTypeParameter(object context, int index) => "!" + index;
    public string GetModifiedType(string modifier, string type, bool required) => type;
    public string GetPinnedType(string element) => element;
    public string GetPointerType(string element) => element + "*";
    public string GetPrimitiveType(PrimitiveTypeCode type) => type.ToString();
    public string GetSZArrayType(string element) => element + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte kind)
    {
        var type = reader.GetTypeDefinition(handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte kind)
    {
        var type = reader.GetTypeReference(handle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
    public string GetTypeFromSpecification(MetadataReader reader, object context, TypeSpecificationHandle handle, byte kind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, context);
    private static string Key(string type, string name, MethodSignature<string> signature) =>
        type + "::" + name + "(" + string.Join(",", signature.ParameterTypes) + ") -> " + signature.ReturnType;
    public static string[] Verify(string baselinePath, string consumerPath)
    {
        var provider = new ApiSignatureProvider();
        var required = new HashSet<string>();
        var referenced = new HashSet<string>();
        using (var stream = File.OpenRead(baselinePath))
        using (var pe = new PEReader(stream))
        {
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                if ((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) continue;
                foreach (var mh in type.GetMethods())
                {
                    var method = reader.GetMethodDefinition(mh);
                    var access = method.Attributes & MethodAttributes.MemberAccessMask;
                    if (access != MethodAttributes.Public && access != MethodAttributes.Family) continue;
                    required.Add(Key(provider.GetTypeFromDefinition(reader, handle, 0), reader.GetString(method.Name), method.DecodeSignature(provider, null)));
                }
            }
        }
        using (var stream = File.OpenRead(consumerPath))
        using (var pe = new PEReader(stream))
        {
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.MemberReferences)
            {
                var member = reader.GetMemberReference(handle);
                if (member.Parent.Kind != HandleKind.TypeReference || member.GetKind() != MemberReferenceKind.Method) continue;
                var type = provider.GetTypeFromReference(reader, (TypeReferenceHandle)member.Parent, 0);
                if (type.StartsWith("Topomatic.ToolBridge."))
                    referenced.Add(Key(type, reader.GetString(member.Name), member.DecodeMethodSignature(provider, null)));
            }
            foreach (var handle in reader.AssemblyReferences)
            {
                var reference = reader.GetAssemblyReference(handle);
                if (reader.GetString(reference.Name) == "Topomatic.ToolBridge" && reference.Version.ToString() != "0.1.0.0")
                    throw new Exception("Consumer does not target ToolBridge 0.1.0.0");
            }
        }
        var result = new List<string>();
        foreach (var member in required) if (!referenced.Contains(member)) result.Add("MISSING " + member);
        result.Insert(0, "Baseline members (public + protected constructor): " + required.Count + "; referenced: " + referenced.Count + "; missing: " + result.Count);
        return result.ToArray();
    }
}
