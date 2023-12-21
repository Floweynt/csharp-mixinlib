using System.Reflection;
using MixinLib.Attributes;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MixinLib.Internal.Processor
{
    [RegisterInjector(typeof(ModifyConstant), InjectorTarget.TARGET_INSTR)]
    public class ModifyConstantProcessor : InjectorProcessor
    {
        private readonly MixinContext Context;
        private readonly ILContext ILContext;
        private readonly MethodBase TargetMethod;
        private readonly Instruction ConstantLoadInstr;
        private readonly Dictionary<MethodBase, MethodBase> MethodRemap;

        public ModifyConstantProcessor(MixinContext mixinContext, ILContext ilContext, MethodBase target, Dictionary<MethodBase, MethodBase> methodRemap,
            StackState?[] _1, StackState stackCurrState, int instrIndex)
        {
            Context = mixinContext;
            ILContext = ilContext;
            TargetMethod = target;
            ConstantLoadInstr = ilContext.Instrs[instrIndex];
            MethodRemap = methodRemap;
        }

        public override IEnumerable<ILBlock> Inject(InjectorInfo injectorInfo)
        {
            var block = new ILBlock(ILContext);
            var instance = (ModifyConstant)injectorInfo.Injector;

            BuildCall(block, TargetMethod, injectorInfo, MethodRemap, (b) =>
            {
                b.Emit(ConstantLoadInstr);
            });

            return Utils.EnumerableOf(block);
        }

        public static ValidateResult ValidateMethodSignature(MixinContext _0, Injector _1, MethodBase _2, MethodBase _3)
        {
            return ValidateOk();
        }

        public static ValidateResult ValidateOpcode(MixinContext _0, Injector _1, MethodBase target, MethodBase body, Instruction instr)
        {
            Type? instrVal = instr.OpCode.Code switch
            {
                Code.Ldc_I4 => typeof(int),
                Code.Ldc_I4_S => typeof(int),
                Code.Ldc_I4_M1 => typeof(int),
                Code.Ldc_I4_0 => typeof(int),
                Code.Ldc_I4_1 => typeof(int),
                Code.Ldc_I4_2 => typeof(int),
                Code.Ldc_I4_3 => typeof(int),
                Code.Ldc_I4_4 => typeof(int),
                Code.Ldc_I4_5 => typeof(int),
                Code.Ldc_I4_6 => typeof(int),
                Code.Ldc_I4_7 => typeof(int),
                Code.Ldc_I4_8 => typeof(int),
                Code.Ldc_I8 => typeof(long),
                Code.Ldc_R4 => typeof(float),
                Code.Ldc_R8 => typeof(double),
                Code.Ldstr => typeof(string),
                _ => null
            };

            if (instrVal == null)
                return ValidateFail("ModifyConstant expects constant load instruction");

            return ValidateMethodSignatureByType(target, body, instrVal, instrVal);
        }
    }
}
