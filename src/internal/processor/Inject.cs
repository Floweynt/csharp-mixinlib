using System.Reflection;
using MixinLib.Attributes;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MixinLib.Internal.Processor
{
    [RegisterInjector(typeof(Inject), InjectorTarget.TARGET_BEFORE)]
    public class InjectProcessor : InjectorProcessor
    {
        private static readonly Type CallbackInfoType = typeof(CallbackInfo);
        private static readonly Type CallbackInfoReturnType = typeof(CallbackInfoReturn);
        private static readonly MethodBase CallbackInfoCtor = CallbackInfoType.GetConstructors()[0];
        private static readonly MethodBase CallbackInfoReturnCtor = CallbackInfoReturnType.GetConstructors()[0];
        private static readonly MethodBase IsCanceledGetter = CallbackInfoType.GetProperty("IsCanceled").GetGetMethod();
        private static readonly MethodBase ReturnValueGetter = CallbackInfoReturnType.GetProperty("ReturnValue").GetGetMethod();

        private readonly ILContext ILContext;
        private readonly Type CallbackType;
        private readonly MethodBase CallbackCtor;
        private readonly MethodBase TargetMethod;
        private readonly bool IsVoidMethod;
        private readonly int CbiLocal;
        private readonly int RetValueLocal = -1;
        private readonly int StackSize;
        private readonly Dictionary<MethodBase, MethodBase> MethodRemap;

        public InjectProcessor(MixinContext _0, ILContext ilContext, MethodBase target, Dictionary<MethodBase, MethodBase> methodRemap,
            StackState?[] _1, StackState stackCurrState, int _2)
        {
            ILContext = ilContext;
            TargetMethod = target;
            IsVoidMethod = target.IsVoidMethod();
            CallbackType = IsVoidMethod ? CallbackInfoType : CallbackInfoReturnType;
            CallbackCtor = IsVoidMethod ? CallbackInfoCtor : CallbackInfoReturnCtor;
            CbiLocal = ILContext.AllocateLocal(CallbackType);
            MethodRemap = methodRemap;
            if (!IsVoidMethod)
                RetValueLocal = ILContext.AllocateLocal(target.ReturnType());
            StackSize = stackCurrState.Count;
        }

        private void MakeReturn(ILBlock block)
        {
            var fin = ILContext.IL.Create(OpCodes.Nop);
            block.Emit(OpCodes.Brfalse, fin);
            block.Emit(StackSize, OpCodes.Pop);

            if (!IsVoidMethod)
            {
                block.Emit(OpCodes.Ldloc, RetValueLocal);
            }

            block.Emit(OpCodes.Ret);
            block.Emit(fin);
        }

        public override IEnumerable<ILBlock> Inject(InjectorInfo injectorInfo)
        {
            var block = new ILBlock(ILContext);
            var instance = (Inject)injectorInfo.Injector;

            // stack state [...] -> [...]
            // locals:
            // new CBI(instance.Cancellable) -> cbiLocal
            block.Emit(instance.Cancellable ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            block.Emit(OpCodes.Newobj, CallbackCtor);
            block.Emit(OpCodes.Stloc, CbiLocal);

            BuildCall(block, TargetMethod, injectorInfo, MethodRemap, (b) =>
            {
                b.Emit(OpCodes.Ldloc, CbiLocal);
            });

            // stack state [...] -> [..., cbiLocal]
            block.Emit(OpCodes.Ldloc, CbiLocal);

            if (IsVoidMethod)
            {
                // stack state [..., cbiLocal] -> [..., shouldReturn]
                block.Emit(OpCodes.Call, IsCanceledGetter);
            }
            else
            {
                // stack state [..., cbiLocal] -> [..., returnValue]
                block.Emit(OpCodes.Call, ReturnValueGetter);
                // stack state [..., returnValue] ->  [..., returnValue, returnValue]
                block.Emit(OpCodes.Dup);
                // stack state [..., returnValue, returnValue] => [..., returnValue]
                // locals:
                // returnValue -> retValueLocal
                block.Emit(OpCodes.Stloc, RetValueLocal);
            }

            // if cancel, return
            // stack state [..., returnValue/flag] => [...]
            MakeReturn(block);
            return Utils.EnumerableOf(block);
        }

        public static ValidateResult ValidateMethodSignature(MixinContext _0, Injector _1, MethodBase target, MethodBase body)
        {
            return ValidateMethodSignatureByType(target, body, typeof(void), target.IsVoidMethod() ? typeof(CallbackInfo) : typeof(CallbackInfoReturn));
        }
    }
}
