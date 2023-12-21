using System.Reflection;
using MixinLib.Internal.Processor;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MixinLib.Internal
{
    class MixinTransformer
    {
        private readonly MixinContext Context;

        public MixinTransformer(MixinContext context)
        {
            Context = context;
        }

        private ILBlock BuildPatchBegin(ILContext context)
        {
            var block = new ILBlock(context, -2);
            block.Emit(OpCodes.Nop);
            if (Context.Config.EnableDebugInjectAnnotations)
            {
                block.Emit(OpCodes.Ldstr, $"debug: mixin start");
                block.Emit(OpCodes.Pop);
                block.Emit(OpCodes.Nop);
            }
            return block;
        }

        private ILBlock BuildPatchEnd(ILContext context)
        {
            var block = new ILBlock(context, -3);
            if (Context.Config.EnableDebugInjectAnnotations)
            {
                block.Emit(OpCodes.Nop);

                block.Emit(OpCodes.Ldstr, $"debug: mixin start");
                block.Emit(OpCodes.Pop);
            }

            block.Emit(OpCodes.Nop);
            return block;
        }

        private struct InstrInjectInfo
        {
            public Dictionary<Type, InjectorProcessor> InjectorProcessors;
            public List<InjectorInfo> InjectBeforeInstr;
            public InjectorInfo? InjectOnInstr;
            public List<InjectorInfo> InjectAfterInstr;

            public readonly void Process(InjectorInfo info, ILBlockContainer container)
            {
                info.InjectionCount++;
                container.Add(InjectorProcessors[info.Injector.GetType()].Inject(info));
            }
        }

        private InstrInjectInfo GetInstrInjectInfo(List<InjectorInfo> injectorInfos,
                ILContext il,
                MethodBase target,
                Dictionary<MethodBase, MethodBase> methodRemap,
                StackState?[] stackStates,
                StackState stackCurrState,
                int instrIndex
            )
        {
            var ret = new InstrInjectInfo()
            {
                InjectorProcessors = new(),
                InjectBeforeInstr = new(),
                InjectAfterInstr = new()
            };

            foreach (var injectorInfo in injectorInfos)
            {
                var injectorType = injectorInfo.Injector.GetType();

                var processorInfo = Context.Registry.GetInjectorProcessorFor(injectorType);
                if (!ret.InjectorProcessors.ContainsKey(injectorType))
                    ret.InjectorProcessors.Add(injectorType, processorInfo.Producer(Context, il, target, methodRemap, stackStates, stackCurrState, instrIndex));

                if (processorInfo.Target == InjectorTarget.TARGET_BEFORE)
                    ret.InjectBeforeInstr.Add(injectorInfo);
                else if (processorInfo.Target == InjectorTarget.TARGET_INSTR)
                {
                    ret.InjectOnInstr ??= injectorInfo;

                    if (ret.InjectOnInstr.Priority < injectorInfo.Injector.Priority)
                    {
                        if (ret.InjectOnInstr.Injector.Required)
                            throw new MixinProcessorException($"{Utils.GetInjDiagId(injectorInfo)}: mixin marked as required, but is overwriten by injector with higher priority");
                        ret.InjectOnInstr = injectorInfo;
                    }
                }
                else if (processorInfo.Target == InjectorTarget.TARGET_AFTER)
                    ret.InjectAfterInstr.Add(injectorInfo);

            }

            return ret;
        }

        private void TransformMethod(MethodBase targetMethod, MixinMethodRemapper remapMethods, DefaultedDict<int, List<InjectorInfo>> injection)
        {
            Context.Logger.Debug($"transforming method: {targetMethod}", IMixinLogger.LogType.TRANSFORM);
            new ILHook(targetMethod, il =>
            {
                var stackStates = StackStateCalculator.ComputeStackState(il.Instrs);
                var remapInfo = remapMethods.RemappedMethods[targetMethod.DeclaringType];
                if (Context.Logger.WillLog(IMixinLogger.LogLevel.DEBUG))
                {
                    Context.Logger.Debug("pre-transform: ", IMixinLogger.LogType.DISASSEMBLE);
                    Context.DumpMethodAsm(Context.Decompiler.FromMonomod(il), (instr, index) =>
                        $"s(-> {stackStates[index]?.Count.ToString() ?? "?"}): {instr}");
                }

                var cursor = new InstructionCursor(il);
                int len = il.Instrs.Count;

                for (int i = 0; i < len; i++)
                {
                    if (injection.TryGetValue(i, out List<InjectorInfo>? injectors))
                    {
                        var currStackState = stackStates[i];
                        if (currStackState == null)
                        {
                            Context.Logger.Warn("injection targets unreachable code (this may also be a StackStateCalculator bug)");
                            continue;
                        }

                        var instrInjectInfo = GetInstrInjectInfo(injectors, il, targetMethod, remapInfo, stackStates, currStackState, i);
                        ILBlockContainer container = new();

                        container.Add(BuildPatchBegin(il));

                        foreach (var beforeInjector in instrInjectInfo.InjectBeforeInstr)
                        {
                            instrInjectInfo.Process(beforeInjector, container);
                        }

                        if (instrInjectInfo.InjectOnInstr != null)
                        {
                            instrInjectInfo.Process(instrInjectInfo.InjectOnInstr, container);
                        }
                        else
                        {
                            container.Add(cursor.Instr);
                        }

                        foreach (var afterInjector in instrInjectInfo.InjectAfterInstr)
                        {
                            instrInjectInfo.Process(afterInjector, container);
                        }

                        container.Add(BuildPatchEnd(il));

                        if (Context.Logger.WillLog(IMixinLogger.LogLevel.DEBUG, IMixinLogger.LogType.DISASSEMBLE))
                        {
                            var printer = Context.Logger.Multiline(IMixinLogger.LogLevel.DEBUG, IMixinLogger.LogType.DISASSEMBLE);
                            printer.AddLine("dumping injected instructions:");
                            foreach (var instr in container.Enumerable())
                            {
                                printer.AddLine($"  {new DecompileInstr(instr):$opc $operand}");
                            }
                            printer.Done();
                        }
                        cursor.Inject(CursorLocation.ON_INSTR, container.Enumerable());
                    }
                    else
                    {
                        cursor.Next();
                    }
                }

                if (Context.Logger.WillLog(IMixinLogger.LogLevel.DEBUG))
                {
                    Context.Logger.Debug("injected bytecode dump: ", IMixinLogger.LogType.DISASSEMBLE);
                    Context.DumpMethodAsm(Context.Decompiler.FromMonomod(il, true), (instr, index) =>
                    {
                        char prefix = instr.Offset switch
                        {
                            -1 => '+',
                            -2 => 'B',
                            -3 => 'E',
                            _ => ' '
                        };

                        return instr.Offset >= 0 ?
                            $"{instr:[$offset]$label $opc $operand}" :
                            $"{prefix}{instr:$label $opc $operand}";
                    });
                }
            }).Apply();

        }

        public void Process(MixinOpSel selectedOps, MixinMethodRemapper remapMethods)
        {
            foreach (var (k, v) in selectedOps.InjectionsToProcess)
            {
                TransformMethod(k, remapMethods, v);
            }
        }
    }
}