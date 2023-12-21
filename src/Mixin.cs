using MixinLib;
using MixinLib.Attributes;

namespace MixinLibTest
{
    public class MixinTargetTest
    {
        public void MethodOne(bool flag)
        {
            // TestHead()
            Console.WriteLine("Hello, World!");

            // TestBeforeEquals()
            if (flag.Equals(false))
            {
                Console.WriteLine("Case 1");
                // TestReturn()
                return;
            }

            Console.WriteLine("Case 2");

            // TestReturn()
            return;
        }
    }

    [Mixin("MixinLibTest.MixinTargetTest")]
    class TestMixin
    {
        [Inject(Cancellable = true, Priority = 100, Disable = true)]
        [Target("MethodOne")]
        [AtHead]
        public void Test1(CallbackInfo _)
        {
            Console.WriteLine("p=100");
        }

        [Inject(Cancellable = true, Priority = 10, Disable = true)]
        [Target("MethodOne")]
        [AtHead]
        public void Test2(CallbackInfo info, bool flag)
        {
            Console.WriteLine("p=10");
        }

        [Inject(Cancellable = true, Disable = true)]
        [Target("MethodOne")]
        [AtHead]
        public void TestHead(CallbackInfo info, bool flag)
        {
            Console.WriteLine(GetType());
            Console.WriteLine("TestHead " + flag);
        }

        [Inject(Disable = true)]
        [Target("MethodOne")]
        [AtTail]
        public static void TestReturn(CallbackInfo info)
        {
            Console.WriteLine("TestReturn");
        }

        [Inject(Disable = true)]
        [Target("MethodOne")]
        [AtInvoke(typeof(bool), "Equals")]
        public static void TestBeforeEquals(CallbackInfo info)
        {
            Console.WriteLine("TestBeforeEquals");
        }

        [ModifyConstant]
        [Target("MethodOne")]
        [AtLoadConst("Hello, World!")]
        public static string TestModifyConstant(string value)
        {
            return value + " -- injected --";
        }
    }

    public class MixinTest
    {
        public static void Main()
        {
            MixinProcessor.Init(new MixinLibConfig
            {
                Logger = new StdoutMixinLogger(
                    IMixinLogger.LogLevel.DEBUG,
                    IMixinLogger.LogType.MISC |
                    IMixinLogger.LogType.DISASSEMBLE |
                    IMixinLogger.LogType.OPSEL |
                    IMixinLogger.LogType.TRANSFORM
                ),
                MixinDumpPath = "./mixin.json",
            });

            MixinProcessor.InjectMixins();

            Console.WriteLine("post-inject");
            new MixinTargetTest().MethodOne(false);
            new MixinTargetTest().MethodOne(true);
        }
    }
}
