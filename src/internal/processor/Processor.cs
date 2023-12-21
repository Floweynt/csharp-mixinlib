using System.Reflection;
using MixinLib.Attributes;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using MonoMod.Cil;

namespace MixinLib.Internal.Processor
{
    public enum InjectorTarget
    {
        TARGET_BEFORE,
        TARGET_INSTR,
        TARGET_AFTER
    }

    public abstract class InjectorProcessor
    {
        public abstract IEnumerable<ILBlock> Inject(InjectorInfo info);

        public struct InjectorProcessorInfo
        {
            public delegate InjectorProcessor Factory(
                MixinContext mixinContext,
                ILContext il,
                MethodBase target,
                Dictionary<MethodBase, MethodBase> methodRemap,
                StackState?[] stackStates,
                StackState stackCurrState,
                int instrIndex
            );

            public delegate ValidateResult ValidateMethodSignature(MixinContext mixinContext, Injector instance, MethodBase target, MethodBase body);
            public delegate ValidateResult ValidateOpcode(MixinContext mixinContext, Injector instance, MethodBase target, MethodBase body, Instruction instr);

            public Type InjectorType;
            public Factory Producer;
            public InjectorTarget Target;
            public ValidateMethodSignature MethodValidator;
            public ValidateOpcode? OpValidator;
        }

        public struct ValidateResult
        {
            public readonly bool Success { get => Message == null; }
            public string? Message;
        }

        public static ValidateResult ValidateFail(string msg) { return new ValidateResult() { Message = msg }; }
        public static ValidateResult ValidateOk() { return new ValidateResult() { Message = null }; }

        protected static void BuildCall(
            ILBlock output, MethodBase target, InjectorInfo info, Dictionary<MethodBase, MethodBase> remap, params Action<ILBlock>?[] entries)
        {
            var remappedMethod = remap[info.Body];
            int maxEmit = remappedMethod.GetParameters().Length;
            int argIndex = 0;

            if (!info.Body.IsStatic)
            {
                output.Emit(OpCodes.Ldarg_0);
                argIndex++;
            }

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    entry(output);
                    argIndex++;
                }
            }

            for (int i = 0; i < target.GetParameters().Count(); i++)
            {
                if (argIndex >= maxEmit)
                    break;

                output.Emit(OpCodes.Ldarg, i + (info.Body.IsStatic ? 0 : 1));
            }

            output.Emit(OpCodes.Call, remappedMethod);
        }

        protected static ValidateResult ValidateMethodSignatureByType(MethodBase target, MethodBase body, Type returnType, params Type[] entries)
        {
            // validate staticness
            if (!body.IsStatic && target.IsStatic)
                return ValidateFail("injection body cannot be instance when targeting static method");

            if (!body.ReturnType().Equals(returnType) && returnType != null)
                return ValidateFail($"return type should be {returnType.FullName}, but got {body.ReturnType().FullName}");

            var bodyParams = body.GetParameters();
            var targetParams = target.GetParameters();

            if (bodyParams.Length < entries.Length)
                return ValidateFail($"injection body is missing parameters: expected at least {entries.Length}, but got {bodyParams.Length}");

            var allParams = entries.Concat(targetParams.Select(u => u.ParameterType));

            if (bodyParams.Length > allParams.Count())
                return ValidateFail($"injection body has too many parameters: max is {allParams.Count()}, but got {bodyParams.Length}");

            int i = 0;
            foreach (var expectedType in allParams)
            {
                if (i >= bodyParams.Length)
                    break;

                var actualType = bodyParams[i].ParameterType;
                if (expectedType != null && !actualType.Equals(expectedType))
                    return ValidateFail($"parameter type mismatch at index {i}: expected {expectedType.FullName} but got {actualType.FullName}");
                i++;
            }

            return ValidateOk();
        }
    }

    public struct OpSelInfo
    {
        public At Instance;
        public Collection<Instruction> Instructions;
        public int Offset;
        public Instruction Target;
    }

    public abstract class SelectorProcessor
    {
        public abstract bool TrySelect(MixinContext context, OpSelInfo info);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RegisterInjector : Attribute
    {
        public readonly Type Type;
        public readonly InjectorTarget Target;
        public RegisterInjector(Type type, InjectorTarget target)
        {
            Type = type;
            Target = target;
        }
    };

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RegisterSelector : Attribute
    {
        public readonly Type Type;
        public RegisterSelector(Type type) { Type = type; }
    };
}
