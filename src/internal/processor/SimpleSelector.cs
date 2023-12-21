using MixinLib.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MixinLib.Internal.Processor
{
    [RegisterSelector(typeof(AtHead))]
    public class AtHeadProcessor : SelectorProcessor
    {
        public override bool TrySelect(MixinContext context, OpSelInfo info)
        {
            return info.Offset == 0;
        }
    }

    [RegisterSelector(typeof(AtTail))]
    public class AtTailProcessor : SelectorProcessor
    {
        public override bool TrySelect(MixinContext context, OpSelInfo info)
        {
            return info.Offset + 1 == info.Instructions.Count;
        }
    }

    [RegisterSelector(typeof(AtReturn))]
    public class AtReturnProcessor : SelectorProcessor
    {
        public override bool TrySelect(MixinContext context, OpSelInfo info)
        {
            return info.Target.OpCode == OpCodes.Ret;
        }
    }

    [RegisterSelector(typeof(AtInvoke))]
    public class AtInvokeProcessor : SelectorProcessor
    {
        public override bool TrySelect(MixinContext context, OpSelInfo info)
        {
            var inst = (AtInvoke)info.Instance;

            if (info.Target.OpCode == OpCodes.Call && inst.Staticness.HasFlag(AtInvoke.FunctionType.Static) ||
                info.Target.OpCode == OpCodes.Callvirt && inst.Staticness.HasFlag(AtInvoke.FunctionType.Member))
            {
                var operands = (MethodReference)info.Target.Operand!;
                var parameters = operands.Parameters;
                if (operands.Name != inst.Name)
                    return false;

                if (operands.DeclaringType.FullName != inst.TargetType.ToString())
                    return false;

                if (inst.Parameters == null)
                    return true;

                if (parameters.Count != inst.Parameters.Length)
                    return false;

                for (int i = 0; i < parameters.Count; i++)
                {
                    if (parameters[i].ParameterType.FullName != inst.Parameters[i].ToString())
                        return false;
                }

                return true;
            }

            return false;
        }
    }

    [RegisterSelector(typeof(AtLoadConst))]
    public class AtLoadConstProcessor : SelectorProcessor
    {
        public override bool TrySelect(MixinContext context, OpSelInfo info)
        {
            var inst = (AtLoadConst)info.Instance;
            object? instrVal = info.Target.OpCode.Code switch
            {
                Code.Ldc_I4 => (int)info.Target.Operand,
                Code.Ldc_I4_S => (int)info.Target.Operand,
                Code.Ldc_I4_M1 => -1,
                Code.Ldc_I4_0 => 0,
                Code.Ldc_I4_1 => 1,
                Code.Ldc_I4_2 => 2,
                Code.Ldc_I4_3 => 3,
                Code.Ldc_I4_4 => 4,
                Code.Ldc_I4_5 => 5,
                Code.Ldc_I4_6 => 6,
                Code.Ldc_I4_7 => 7,
                Code.Ldc_I4_8 => 8,
                Code.Ldc_I8 => (long)info.Target.Operand,
                Code.Ldc_R4 => (float)info.Target.Operand,
                Code.Ldc_R8 => (double)info.Target.Operand,
                Code.Ldstr => (string)info.Target.Operand,
                _ => null
            };

            if (instrVal == null)
                return false;

            if (!instrVal.GetType().Equals(inst.Value.GetType()))
                return false;

            return instrVal.Equals(inst.Value);
        }
    }
}
