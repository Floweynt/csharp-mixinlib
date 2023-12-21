using MixinLib.Attributes;
using MixinLib.Internal;

namespace MixinLib
{
    public class MixinProcessorException : Exception
    {
        public MixinProcessorException()
        {
        }

        public MixinProcessorException(string message)
            : base(message)
        {
        }

        public MixinProcessorException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public static void Report(MixinContext context, SelectFailureMode mode, string msg)
        {
            if (mode == SelectFailureMode.FAIL_EXCEPTION)
                throw new MixinProcessorException(msg);
            if (mode == SelectFailureMode.FAIL_MESSAGE)
                context.Logger.Error(msg);
        }
    }

    public class MixinLibConfig
    {
        public bool EnableDebugInjectAnnotations = false;
        public IMixinLogger Logger = new StdoutMixinLogger();
        public string? MixinDumpPath = null;

        public MixinLibConfig()
        {
        }
    }

    public class MixinProcessor
    {
        private static MixinContext? Context;
        private static MixinLibConfig? Config;

        public static void Init(MixinLibConfig? config)
        {
            Config = config ?? new MixinLibConfig();

            Context = new MixinContext(Config);
            Context.Registry.AutoRegister(Context);
        }

        public static void InjectMixins()
        {
            // Mixin transformation has 3 phases:
            // 1. Scan mixin classes (scan)
            // 2. Select opcodes (opsel)
            // 3.a. Copy mixin methods (remap)
            // 3.b. Apply bytecode injection (transform)
            if (Context == null || Config == null)
                throw new InvalidOperationException("InjectMixins called before Init");

            MixinScanner scanner = new(Context);
            Context.Logger.Info("MixinScanner: scanning for mixins", IMixinLogger.LogType.SCAN);
            var mixinInfo = scanner.ScanForMixins();
            Context.Logger.Info($"MixinScanner: found {mixinInfo.Count} mixins", IMixinLogger.LogType.SCAN);

            // dump selected mixins
            var mixinPath = Config.MixinDumpPath;
            if (mixinPath != null)
            {
                Context.Logger.Info($"dumping loaded mixin information to {mixinPath}", IMixinLogger.LogType.SCAN);
                File.WriteAllText(mixinPath, Utils.DumpJson(mixinInfo));
            }

            Context.Logger.Info($"MixinOpSel: selecting instructions to inject", IMixinLogger.LogType.OPSEL);
            MixinOpSel opSel = new(Context);
            foreach (var mixin in mixinInfo)
            {
                opSel.SelectMixin(mixin);
            }

            foreach (var (_, offsets) in opSel.InjectionsToProcess)
            {
                foreach (var (_, val) in offsets)
                {
                    val.Sort((lhs, rhs) => rhs.Priority - lhs.Priority);
                }
            }

            if (Context.Logger.WillLog(IMixinLogger.LogLevel.DEBUG))
            {
                foreach (var (method, offsets) in opSel.InjectionsToProcess)
                {
                    Context.Logger.Debug($"dumping selected instructions from method {method.Name}: ", IMixinLogger.LogType.DISASSEMBLE);

                    Context.DumpMethodAsm(Context.Decompiler.FromMonomod(Context.ContextFromMethod(method)), (instr, index) =>
                        $"{(offsets.ContainsKey(index) ? "-> " : "")}{instr}");
                }
            }

            Context.Logger.Info($"MixinTransformer: transforming bytecode", IMixinLogger.LogType.TRANSFORM);
            MixinMethodRemapper remapper = new(Context);
            remapper.Handle(mixinInfo);
            MixinTransformer transformer = new(Context);
            transformer.Process(opSel, remapper);

            foreach (var mixin in mixinInfo)
            {
                foreach (var injector in mixin.Injectors)
                {
                    var count = injector.InjectionCount;
                    var maxCount = injector.Injector.MaxInjections;
                    var minCount = injector.Injector.MinInjections;
                    if (maxCount > 0 && count > maxCount)
                    {
                        Context.Logger.Warn($"{Utils.GetInjDiagId(injector)}: injector specifies a max of {maxCount} injections, but {count} were found");
                    }
                    if (count < minCount)
                    {
                        Context.Logger.Warn($"{Utils.GetInjDiagId(injector)}: injector specifies a min of {minCount} injections, but {count} were found");
                    }
                }
            }
        }
    }
}