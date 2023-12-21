using System.Reflection;
using System.Text;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MixinLib
{
    public abstract class IMixinLogger
    {
        private readonly LogLevel Level;
        private readonly LogType EnabledTypes;

        public virtual bool WillLog(LogLevel level, LogType type = LogType.MISC)
        {
            return level <= Level && (EnabledTypes.HasFlag(type) || level <= LogLevel.INFO);
        }

        private void DoLog(LogLevel level, string msg, LogType type)
        {
            if (WillLog(level, type))
            {
                Log(level, msg, type);
            }
        }

        protected IMixinLogger(LogLevel maxLevel, LogType type)
        {
            Level = maxLevel;
            EnabledTypes = type;
        }

        public enum LogLevel
        {
            ERROR,
            WARN,
            INFO,
            DEBUG,
            TRACE
        };

        [Flags]
        public enum LogType
        {
            MISC = 1,
            DISASSEMBLE = 2,
            REGISTERY = 4,
            SCAN = 8,
            OPSEL = 16,
            TRANSFORM = 32,
        };

        public class LogHelper
        {
            private readonly StringBuilder Builder = new();
            private readonly IMixinLogger MyLogger;
            private readonly LogLevel Level;
            private readonly LogType Type;

            public LogHelper(IMixinLogger logger, LogLevel level, LogType type)
            {
                MyLogger = logger;
                Level = level;
                Type = type;
            }

            public void AddLine(string msg) { Builder.AppendLine(msg); }
            public void Done() { MyLogger.DoLog(Level, Builder.ToString(), Type); }
        }

        public void Error(string msg, LogType type = LogType.MISC) => DoLog(LogLevel.ERROR, msg, type);
        public void Warn(string msg, LogType type = LogType.MISC) => DoLog(LogLevel.WARN, msg, type);
        public void Info(string msg, LogType type = LogType.MISC) => DoLog(LogLevel.INFO, msg, type);
        public void Debug(string msg, LogType type = LogType.MISC) => DoLog(LogLevel.DEBUG, msg, type);
        public void Trace(string msg, LogType type = LogType.MISC) => DoLog(LogLevel.TRACE, msg, type);
        public LogHelper Multiline(LogLevel level, LogType type = LogType.MISC) => new(this, level, type);
        public abstract void Log(LogLevel level, string msg, LogType type);
    }

    public class StdoutMixinLogger : IMixinLogger
    {
        public StdoutMixinLogger(LogLevel level = LogLevel.INFO, LogType type =
            LogType.MISC |
            LogType.REGISTERY |
            LogType.SCAN |
            LogType.OPSEL |
            LogType.TRANSFORM
        ) : base(level, type) { }
        override public void Log(LogLevel level, string msg, LogType type)
            => Console.WriteLine($"[mixin] [{level.ToString().ToLower()}] [{type.ToString().ToLower()}]: {msg}");
    }

    namespace Internal
    {
        // used for module scanning, for the most part
        public class MixinContext
        {
            public readonly Assembly[] Assemblies;
            public IMixinLogger Logger { get => Config.Logger; }
            private readonly Cache<string, Type?> TypeLookupCache = new();
            private readonly Cache<MethodBase, ILContext> ContextCache = new();
            public readonly DecompiledMethod.Decompiler Decompiler;
            public readonly MixinLibConfig Config;
            public readonly ProcessorRegistry Registry = new();

            public MixinContext(MixinLibConfig config) : this(AppDomain.CurrentDomain.GetAssemblies(), config)
            {
            }

            public MixinContext(Assembly[] assemblies, MixinLibConfig config)
            {
                Assemblies = assemblies;
                Decompiler = new(this);
                Config = config;
            }

            public Type? GetTypeByName(string typeName)
            {
                return TypeLookupCache.Get(typeName, typeName =>
                {
                    var type = Type.GetType(typeName);
                    if (type != null)
                        return type;

                    foreach (var a in Assemblies)
                    {
                        type = a.GetType(typeName);
                        if (type != null)
                            return type;
                    }

                    return null;
                });
            }

            public ILContext ContextFromMethod(MethodBase method)
            {
                return ContextCache.Get(method, method =>
                {
                    return new ILContext(new DynamicMethodDefinition(method).Definition);
                });
            }

            public IEnumerable<(Type, object[])> GetTypesWithAttribute(Type attr)
            {
                foreach (Assembly assembly in Assemblies)
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        var attrs = type.GetCustomAttributes(attr, true);
                        if (attrs.Length > 0)
                        {
                            yield return (type, attrs);
                        }
                    }
                }
            }

            public void DumpMethodAsm(DecompiledMethod method, Func<DecompileInstr, int, string>? serializer = null,
                IMixinLogger.LogLevel level = IMixinLogger.LogLevel.DEBUG)
            {
                var printer = Logger.Multiline(level, IMixinLogger.LogType.DISASSEMBLE);
                printer.AddLine("instruction dump: ");
                int index = 0;

                var padLen = (method.Instructions.Count - 1).ToString().Count();

                foreach (var instr in method.Instructions)
                {
                    printer.AddLine($"  {index.ToString().PadRight(padLen)} | " + (serializer != null ? serializer(instr, index) : instr.ToString()));
                    index++;
                }
                printer.Done();
            }
        }
    }
}