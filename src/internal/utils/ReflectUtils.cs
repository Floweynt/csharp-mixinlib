using System.Reflection;
using System.Reflection.Emit;
using MixinLib.Attributes;

namespace MixinLib.Internal
{
    public static partial class Utils
    {
        public static MethodBase[] ScanMethods(Type type)
        {
            return Array.FindAll(type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic
            ), ent => ent.DeclaringType.Equals(type));
        }

        public static bool IsVoidMethod(this MethodBase method)
        {
            return method is not MethodInfo info || info.ReturnType == typeof(void);
        }

        public static Type ReturnType(this MethodBase method)
        {
            return method is MethodInfo info ? info.ReturnType : typeof(void);
        }

        public static string GetFullName(this MethodBase method)
        {
            var param = string.Join(", ", method.GetParameters().Select(u => u.GetType().FullName));

            return method switch
            {
                DynamicMethod dyn => $"{dyn.ReturnType.FullName} <dynamic>({param}) `${dyn.Name}`",
                MethodInfo info => $"{info.ReturnType.FullName} {info.DeclaringType.FullName}::{info.Name}({param})",
                _ => $" {method.DeclaringType.FullName}::{method.Name}({param})",
            };
        }

        public static Type Resolve(this TypeDescriptor inst, MixinContext context)
        {
            if (inst.Type == null)
            {
                inst.Type = context.GetTypeByName(inst.Name!) ?? throw new MixinProcessorException("Failed to resolve type: " + inst.Name);
            }

            return inst.Type;
        }

        public static Type? TryResolve(this TypeDescriptor inst, MixinContext context)
        {
            if (inst.Type == null)
            {
                inst.Type = context.GetTypeByName(inst.Name!);
            }

            return inst.Type;
        }
    }
}