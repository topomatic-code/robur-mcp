using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ToolBridge.Tests.ApiCheck_v0_1_0
{
    // Metadata inspection never invokes ToolBridge code or initializes its static state.
    internal static class ApiContract
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        internal static void Verify(string name, string baseType, TypeAttributes baseline,
            string[] expectedMethods, string[] expectedProperties)
        {
            var type = typeof(Topomatic.ToolBridge.ToolProvider).Assembly.GetType(name);
            if (type == null)
            {
                Test.Fail("Недоступен публичный тип " + name);
                return;
            }
            Test.Assert(type.FullName == name && type.IsPublic, "Недоступен публичный тип " + name);
            Test.Assert(type.IsClass && !type.IsGenericType, "Изменён вид типа " + name);
            Test.Assert(HasBase(type, baseType), "Изменён базовый тип " + name + ": ожидался " + baseType);
            if ((baseline & TypeAttributes.Sealed) == 0)
                Test.Assert(!type.IsSealed, "Запрещено наследование от " + name);
            if ((baseline & TypeAttributes.Abstract) == 0)
                Test.Assert(!type.IsAbstract, "Тип стал абстрактным: " + name);
            if ((baseline & (TypeAttributes.Abstract | TypeAttributes.Sealed)) ==
                (TypeAttributes.Abstract | TypeAttributes.Sealed))
                Test.Assert(type.IsAbstract && type.IsSealed, "Тип перестал быть статическим: " + name);

            var methods = type.GetMethods(Members).Cast<MethodBase>()
                .Concat(type.GetConstructors(Members)).ToArray();
            foreach (var expected in expectedMethods)
            {
                var isPublic = expected.StartsWith("public ", StringComparison.Ordinal);
                var signature = expected.Substring(expected.IndexOf(' ') + 1);
                var candidates = methods.Where(m => MethodSignature(m) == signature);
                Test.Assert(candidates.Any(m => IsAccessible(m, isPublic) && !m.IsAbstract),
                    name + ": недоступен член " + expected);
            }

            // New abstract members would break concrete subclasses compiled against 0.1.0.
            if ((baseline & TypeAttributes.Sealed) == 0)
                foreach (var method in methods.Where(m => m.IsAbstract))
                    Test.Fail(name + ": добавлен абстрактный член " + MethodSignature(method));

            var properties = new HashSet<string>(type.GetProperties(Members).Select(PropertySignature), StringComparer.Ordinal);
            foreach (var expected in expectedProperties)
                Test.Assert(properties.Contains(expected), name + ": недоступно свойство " + expected);
        }

        private static bool IsAccessible(MethodBase method, bool requirePublic) =>
            method.IsPublic || (!requirePublic && (method.IsFamily || method.IsFamilyOrAssembly));

        private static bool HasBase(Type type, string expected)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
                if (current.FullName == expected) return true;
            return false;
        }

        private static string MethodSignature(MethodBase member)
        {
            var method = member as MethodInfo;
            var generic = member.IsGenericMethod ? member.GetGenericArguments() : Type.EmptyTypes;
            var name = member.Name + (generic.Length == 0 ? "" : "<" + string.Join(",", generic.Select(TypeName)) + ">");
            var signature = (member.IsStatic ? "static " : "instance ") + name +
                "(" + string.Join(",", member.GetParameters().Select(p => TypeName(p.ParameterType))) + ") -> " +
                (method == null ? "Void" : TypeName(method.ReturnType));
            foreach (var argument in generic)
                signature += " where " + TypeName(argument) + ":" + argument.GenericParameterAttributes +
                    "[" + string.Join(",", argument.GetGenericParameterConstraints().Select(TypeName).OrderBy(n => n, StringComparer.Ordinal)) + "]";
            return signature;
        }

        private static string PropertySignature(PropertyInfo property)
        {
            var accessor = property.GetGetMethod(true) ?? property.GetSetMethod(true);
            return (accessor.IsStatic ? "static " : "instance ") + property.Name +
                "(" + string.Join(",", property.GetIndexParameters().Select(p => TypeName(p.ParameterType))) +
                ") -> " + TypeName(property.PropertyType);
        }

        private static string TypeName(Type type)
        {
            if (type.IsGenericParameter)
                return (type.DeclaringMethod == null ? "!" : "!!") + type.GenericParameterPosition;
            if (type.IsByRef) return TypeName(type.GetElementType()) + "&";
            if (type.IsPointer) return TypeName(type.GetElementType()) + "*";
            if (type.IsArray)
                return TypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericType)
                return type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
            if (type.IsPrimitive || type == typeof(void) || type == typeof(string) || type == typeof(object))
                return type.Name;
            return type.FullName;
        }
    }
}
