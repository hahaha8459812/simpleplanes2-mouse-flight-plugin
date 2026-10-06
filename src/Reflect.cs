using System;
using System.Collections.Generic;
using System.Reflection;

namespace SimplePlanes2MouseFlight
{
    // 所有游戏类型与成员都按字符串名反射访问，插件不引用任何游戏程序集。
    // 反射结果只缓存成功项，失败不缓存；找不到成员只打一次警告，不让插件加载失败。
    // 约定依据：共享文档 docs/build-and-install.md 第一节。
    internal static class Reflect
    {
        private const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
        private static readonly Dictionary<string, MemberInfo> MemberCache = new Dictionary<string, MemberInfo>();
        private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();

        public static Type FindType(string fullName)
        {
            Type cached;

            if (TypeCache.TryGetValue(fullName, out cached))
            {
                return cached;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = null;
                try
                {
                    found = assemblies[i].GetType(fullName, false);
                }
                catch (Exception)
                {
                    found = null;
                }

                if (found != null)
                {
                    TypeCache[fullName] = found;
                    return found;
                }
            }

            return null;
        }

        private static MemberInfo ResolveMember(Type type, string memberName)
        {
            string key = type.FullName + "|" + memberName;
            MemberInfo cached;

            if (MemberCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            MemberInfo found = null;
            try
            {
                found = type.GetProperty(memberName, AllMembers);
            }
            catch (AmbiguousMatchException)
            {
                found = null;
            }

            if (found == null)
            {
                try
                {
                    found = type.GetField(memberName, AllMembers);
                }
                catch (AmbiguousMatchException)
                {
                    found = null;
                }
            }

            if (found == null)
            {
                // 私有字段不会随继承暴露，所以要沿基类链逐层用 DeclaredOnly 找一次。
                Type baseType = type.BaseType;
                while (found == null && baseType != null && baseType != typeof(object))
                {
                    try
                    {
                        found = baseType.GetProperty(
                            memberName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    }
                    catch (AmbiguousMatchException)
                    {
                        found = null;
                    }

                    if (found == null)
                    {
                        try
                        {
                            found = baseType.GetField(
                                memberName,
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        }
                        catch (AmbiguousMatchException)
                        {
                            found = null;
                        }
                    }

                    baseType = baseType.BaseType;
                }
            }

            if (found != null)
            {
                MemberCache[key] = found;
            }

            return found;
        }

        private static MethodInfo ResolveMethod(Type type, string methodName, int parameterCount)
        {
            string key = type.FullName + "|" + methodName + "/" + parameterCount;
            MethodInfo cached;

            if (MethodCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            MethodInfo found = null;
            MethodInfo[] candidates = type.GetMethods(AllMembers);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Name == methodName && candidates[i].GetParameters().Length == parameterCount)
                {
                    found = candidates[i];
                    break;
                }
            }

            if (found != null)
            {
                MethodCache[key] = found;
            }

            return found;
        }

        // 用 Activator 构造游戏类型（例如我们自己再要一个追尾镜头对象）。
        public static bool TryConstruct(string typeName, object[] args, out object instance, out string error)
        {
            Type type;

            instance = null;

            if (!TryResolve(typeName, out type, out error))
            {
                return false;
            }

            try
            {
                instance = Activator.CreateInstance(type, args);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = "constructing " + typeName + " threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static bool TryGetMember(object instance, string typeName, string memberName, out object value, out string error)
        {
            Type type;
            MemberInfo member;

            value = null;
            error = null;

            if (!TryResolve(typeName, out type, out error))
            {
                return false;
            }

            member = ResolveMember(type, memberName);
            if (member == null)
            {
                error = "member not found: " + typeName + "." + memberName;
                return false;
            }

            try
            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    if (!property.CanRead)
                    {
                        error = "property is not readable: " + typeName + "." + memberName;
                        return false;
                    }

                    value = property.GetValue(instance, null);
                    return true;
                }

                FieldInfo field = (FieldInfo)member;
                value = field.GetValue(instance);
                return true;
            }
            catch (Exception ex)
            {
                error = "reading " + typeName + "." + memberName + " threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static bool TryGetStatic(string typeName, string memberName, out object value, out string error)
        {
            return TryGetMember(null, typeName, memberName, out value, out error);
        }

        public static bool TrySetMember(object instance, string typeName, string memberName, object value, out string error)
        {
            Type type;
            MemberInfo member;

            error = null;

            if (!TryResolve(typeName, out type, out error))
            {
                return false;
            }

            member = ResolveMember(type, memberName);
            if (member == null)
            {
                error = "member not found: " + typeName + "." + memberName;
                return false;
            }

            try
            {
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    if (!property.CanWrite)
                    {
                        error = "property is not writable: " + typeName + "." + memberName;
                        return false;
                    }

                    property.SetValue(instance, value, null);
                    return true;
                }

                ((FieldInfo)member).SetValue(instance, value);
                return true;
            }
            catch (Exception ex)
            {
                error = "writing " + typeName + "." + memberName + " threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static bool TryCall(object instance, string typeName, string methodName, int parameterCount, object[] args, out object result, out string error)
        {
            Type type;
            MethodInfo method;

            result = null;
            error = null;

            if (!TryResolve(typeName, out type, out error))
            {
                return false;
            }

            method = ResolveMethod(type, methodName, parameterCount);
            if (method == null)
            {
                error = "method not found: " + typeName + "." + methodName + "/" + parameterCount;
                return false;
            }

            try
            {
                result = method.Invoke(instance, args);
                return true;
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                error = "calling " + typeName + "." + methodName + " threw " + inner.GetType().Name + ": " + inner.Message;
                return false;
            }
        }

        private static bool TryResolve(string typeName, out Type type, out string error)
        {
            type = FindType(typeName);
            if (type == null)
            {
                error = "type not found: " + typeName;
                return false;
            }

            error = null;
            return true;
        }
    }
}
