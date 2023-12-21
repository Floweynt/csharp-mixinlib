using System.Reflection;
using MixinLib.Attributes;
using MixinLib.Internal.Processor;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using static MixinLib.Internal.Processor.InjectorProcessor;

namespace MixinLib.Internal
{
    public class ProcessorRegistry
    {
        private readonly Dictionary<Type, SelectorProcessor> SelectorProcessorRegistry = new();
        private readonly Dictionary<Type, InjectorProcessorInfo> InjectorProcessorRegistry = new();

        public void RegisterSelectorProcessor(Type ty, SelectorProcessor processor) { SelectorProcessorRegistry.Add(ty, processor); }
        public SelectorProcessor GetSelectorProcessorFor(Type type) { return SelectorProcessorRegistry[type]; }
        public void RegisterInjectorProcessor(Type ty, InjectorProcessorInfo processor) { InjectorProcessorRegistry.Add(ty, processor); }
        public InjectorProcessorInfo GetInjectorProcessorFor(Type type) { return InjectorProcessorRegistry[type]; }

        public void AutoRegister(MixinContext context)
        {
            context.Logger.Debug("registering injectors:", IMixinLogger.LogType.REGISTERY);
            foreach (var (processor, annotations) in context.GetTypesWithAttribute(typeof(RegisterInjector)))
            {
                try
                {
                    var registerAnnotation = (RegisterInjector)annotations[0];
                    var type = registerAnnotation.Type;
                    context.Logger.Debug($"registering {processor} for type {type}", IMixinLogger.LogType.REGISTERY);

                    if (!typeof(InjectorProcessor).IsAssignableFrom(processor))
                    {
                        context.Logger.Warn($"failed to register {processor} for type {type}, ignoring", IMixinLogger.LogType.REGISTERY);
                        continue;
                    }

                    var ctor = processor.GetConstructor(new[]{
                        typeof(MixinContext ),
                        typeof(ILContext ),
                        typeof(MethodBase ),
                        typeof(Dictionary<MethodBase, MethodBase> ),
                        typeof(StackState?[] ),
                        typeof(StackState ),
                        typeof(int)
                    });

                    var validateMethod = processor.GetMethod("ValidateMethodSignature", BindingFlags.Static | BindingFlags.Public, null, new[]{
                        typeof(MixinContext ),
                        typeof(Injector ),
                        typeof(MethodBase ),
                        typeof(MethodBase )
                    }, null);

                    var validateOpcode = processor.GetMethod("ValidateOpcode", BindingFlags.Static | BindingFlags.Public, null, new[]{
                        typeof(MixinContext ),
                        typeof(Injector ),
                        typeof(MethodBase ),
                        typeof(MethodBase ),
                        typeof(Instruction)
                    }, null);

                    if (ctor == null)
                    {
                        context.Logger.Warn($"failed to register {processor} for type {type}, ignoring: no matching ctor", IMixinLogger.LogType.REGISTERY);
                        continue;
                    }

                    if (validateMethod == null || validateMethod.ReturnType != typeof(InjectorProcessor.ValidateResult))
                    {
                        context.Logger.Warn($"failed to register {processor} for type {type}, ignoring: no matching method validator", IMixinLogger.LogType.REGISTERY);
                        continue;
                    }

                    if (validateOpcode != null && validateOpcode.ReturnType != typeof(InjectorProcessor.ValidateResult))
                    {
                        context.Logger.Warn($"failed to register {processor} for type {type}, ignoring: no matching opcode validator", IMixinLogger.LogType.REGISTERY);
                        continue;
                    }

                    RegisterInjectorProcessor(type, new InjectorProcessor.InjectorProcessorInfo()
                    {
                        InjectorType = processor,
                        Producer = (
                            MixinContext mixinContext,
                            ILContext il,
                            MethodBase target,
                            Dictionary<MethodBase, MethodBase> methodRemap,
                            StackState?[] stackStates,
                            StackState stackCurrState,
                            int instrIndex
                        ) => (InjectorProcessor)ctor.Invoke(new object[] {
                            mixinContext,
                            il, target,
                            methodRemap,
                            stackStates,
                            stackCurrState,
                            instrIndex
                        }),
                        Target = registerAnnotation.Target,
                        MethodValidator = (MixinContext mixinContext, Injector instance, MethodBase target, MethodBase body)
                            => (InjectorProcessor.ValidateResult)validateMethod.Invoke(null, new object[] { mixinContext, instance, target, body }),
                        OpValidator = validateOpcode == null ? null :
                                 (MixinContext mixinContext, Injector instance, MethodBase target, MethodBase body, Instruction instr)
                                     => (InjectorProcessor.ValidateResult)validateOpcode.Invoke(null, new object[] { mixinContext, instance, target, body, instr })
                    });
                }
                catch (Exception e)
                {
                    context.Logger.Warn($"failed to register {processor}, ignoring", IMixinLogger.LogType.REGISTERY);
                    context.Logger.Warn(e.ToString(), IMixinLogger.LogType.REGISTERY);
                }
            }

            context.Logger.Debug("registering selectors:", IMixinLogger.LogType.REGISTERY);
            foreach (var (processor, annotations) in context.GetTypesWithAttribute(typeof(RegisterSelector)))
            {
                try
                {
                    var type = ((RegisterSelector)annotations[0]).Type;
                    context.Logger.Debug($"registering {processor} for type {type}", IMixinLogger.LogType.REGISTERY);

                    var instance = Activator.CreateInstance(processor);

                    if (instance is null or not SelectorProcessor)
                    {
                        context.Logger.Warn($"failed to register {processor} for type {type}, ignoring", IMixinLogger.LogType.REGISTERY);
                        continue;
                    }

                    RegisterSelectorProcessor(type, (SelectorProcessor)instance);
                }
                catch (Exception e)
                {
                    context.Logger.Warn($"failed to register {processor}, ignoring", IMixinLogger.LogType.REGISTERY);
                    context.Logger.Warn(e.ToString(), IMixinLogger.LogType.REGISTERY);
                }
            }
        }
    }
}