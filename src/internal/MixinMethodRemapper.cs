using System.Reflection;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MixinLib.Internal
{
    class MixinMethodRemapper
    {
        private readonly MixinContext Context;

        public readonly DefaultedDict<Type, Dictionary<MethodBase, MethodBase>> RemappedMethods = new();

        public MixinMethodRemapper(MixinContext context)
        {
            Context = context;
        }

        private void Process(Type _0, Type targetType, MethodBase injectedMethod)
        {
            Context.Logger.Debug($"remap method {injectedMethod.GetFullName()} -> {targetType}", IMixinLogger.LogType.TRANSFORM);
            var dynMethod = new DynamicMethodDefinition(injectedMethod);

            var ctx = new ILContext(dynMethod.Definition);

            // for static, we only need to handle shadows, no need to remap arguments
            if (!injectedMethod.IsStatic)
            {
                dynMethod.Definition.Parameters[0] = new Mono.Cecil.ParameterDefinition(ctx.Import(targetType));
            }

            var genMethod = dynMethod.Generate();
            RemappedMethods[targetType][injectedMethod] = genMethod;
        }

        public void Handle(List<MixinInfo> info)
        {
            // we build a table
            // mixinType + type + method -> method
            foreach (var mixinInfo in info)
            {
                foreach (var targetType in mixinInfo.TargetClasses)
                {
                    foreach (var methodToInject in mixinInfo.MethodsToRemap)
                    {
                        Process(mixinInfo.MixinType, targetType, methodToInject);
                    }
                }
            }
        }
    }
}
