using System.Reflection;
using MixinLib.Attributes;
using MixinLib.Internal.Processor;
using Mono.Cecil.Cil;

namespace MixinLib.Internal
{
    public class MixinOpSel
    {
        private readonly MixinContext Context;

        public MixinOpSel(MixinContext context)
        {
            Context = context;
        }

        public readonly DefaultedDict<
            MethodBase,
            DefaultedDict<
                int,
                List<InjectorInfo>
            >
        > InjectionsToProcess = new();

        private void InsertInjectionPoint(MethodBase method, int offset, InjectorInfo info)
        {
            InjectionsToProcess[method][offset].Add(info);
        }

        private static Dictionary<string, List<MethodBase>> GenerateMethodLUT(Type type)
        {
            var methods = Utils.ScanMethods(type);

            Dictionary<string, List<MethodBase>> methodLookup = new();
            foreach (MethodBase method in methods)
            {
                if (!methodLookup.TryGetValue(method.Name, out List<MethodBase>? value))
                {
                    value = new();
                    methodLookup.Add(method.Name, value);
                }

                value.Add(method);
            }

            return methodLookup;
        }

        private void SelectOpcodes(MethodBase method, InjectorInfo info, InjectorProcessor.InjectorProcessorInfo.ValidateOpcode? validateOpcode)
        {
            var instrs = Context.ContextFromMethod(method).Instrs;
            var instrOrdinal = new int[info.Selectors.Count()].Fill(-1);
            var hasSelected = new bool[info.Selectors.Count()];

            for (int i = 0; i < instrs.Count; i++)
            {
                var instr = instrs[i];


                info.Selectors.Each((selector, selIndex) =>
                {
                    if (Context.Registry.GetSelectorProcessorFor(selector.GetType()).TrySelect(Context, new OpSelInfo()
                    {
                        Instance = selector,
                        Instructions = instrs,
                        Offset = i,
                        Target = instr,
                    }))
                    {
                        instrOrdinal[selIndex]++;
                        if (instrOrdinal[selIndex] == selector.Ordinal || selector.Ordinal == -1)
                        {
                            var shiftedOpIndex = i + selector.Shift;
                            if (shiftedOpIndex < 0 || shiftedOpIndex >= instrs.Count)
                            {
                                Context.Logger.Error($"{Utils.GetSelDiagId(selector, info)}: OpCode shift out-of-bounds: {shiftedOpIndex} (the method body has {instrs.Count} instructions). Ignoring...");
                            }
                            else
                            {
                                hasSelected[selIndex] = true;
                                if (validateOpcode != null)
                                {
                                    var res = validateOpcode(Context, info.Injector, method, info.Body, instr);
                                    if (!res.Success)
                                    {
                                        Context.Logger.Error($"{Utils.GetSelDiagId(selector, info)}: Selected instruction does not work: {res.Message}");
                                    }
                                }
                                InsertInjectionPoint(method, i, info);
                            }
                        }
                    }
                });
            }

            info.Selectors.Each((selector, selIndex) =>
            {
                if (!hasSelected[selIndex])
                {
                    Context.Logger.Warn($"{Utils.GetSelDiagId(selector, info)}: selector is unused; is this expected?");
                }
            });
        }

        private List<MethodBase> SelectMethods(Dictionary<string, List<MethodBase>> methodLookup, Type type, Target[] targets)
        {
            List<MethodBase> info = new();
            foreach (var target in targets)
            {
                if (!methodLookup.ContainsKey(target.Name))
                {
                    MixinProcessorException.Report(Context, target.FailMode, $"failed to find method ${target.Name} in ${type.FullName}");
                    continue;
                }

                var overloads = methodLookup[target.Name];

                if (target.Arguments == null)
                {
                    info.AddRange(overloads);
                    continue;
                }

                bool hasMatched = false;
                foreach (var overload in overloads)
                {
                    var parameters = overload.GetParameters().Select(value => value.ParameterType);

                    if (!Enumerable.SequenceEqual(parameters, target.Arguments.Select(val => val.Resolve(Context))))
                    {
                        continue;
                    }

                    info.Add(overload);
                    hasMatched = true;
                    continue;
                }

                if (!hasMatched)
                {
                    MixinProcessorException.Report(Context, target.FailMode,
                        $"failed to find method {target.Name} in {type.FullName} with the correct signature! (target is {target})");
                }
            }

            return info;
        }

        private void SelectTarget(MixinInfo mixinInfo, Type type)
        {
            Dictionary<string, List<MethodBase>> methodLookup = GenerateMethodLUT(type);

            foreach (var injectorInfo in mixinInfo.Injectors)
            {
                var selectedMethods = SelectMethods(methodLookup, type, injectorInfo.Targets);
                Context.Logger.Debug($"{injectorInfo.Body.Name} matches {string.Join(", ", selectedMethods)}", IMixinLogger.LogType.OPSEL);

                var processor = Context.Registry.GetInjectorProcessorFor(injectorInfo.Injector.GetType());

                foreach (var selectedMethod in selectedMethods)
                {
                    var result = processor.MethodValidator(Context, injectorInfo.Injector, selectedMethod, injectorInfo.Body);
                    if (!result.Success)
                        Context.Logger.Error($"{Utils.GetInjDiagId(injectorInfo)}: failed to select method {selectedMethod.GetFullName()}: {result.Message}");
                    else
                        SelectOpcodes(selectedMethod, injectorInfo, processor.OpValidator);
                }
            }
        }

        public void SelectMixin(MixinInfo mixin)
        {
            foreach (var type in mixin.TargetClasses)
            {
                SelectTarget(mixin, type);
            }
        }
    };
}