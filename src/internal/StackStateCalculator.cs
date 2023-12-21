using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using MonoMod.Cil;

namespace MixinLib.Internal
{
    public class StackState
    {
        public int Count { get; private set; }
        public StackState(int count) { Count = count; }

        public bool Equals(StackState rhs)
        {
            return Count == rhs.Count;
        }
    }

    readonly struct StackStateCalculator
    {
        private readonly StackState?[] State;
        private readonly Queue<KeyValuePair<int, StackState>> InstrsToProcess;

        private bool TrySet(int index, StackState state)
        {
            var prevState = State[index];
            if (prevState == null)
            {
                State[index] = state;
                return true;
            }

            if (!prevState.Equals(state))
                throw new MixinProcessorException("failed computing stack state, make sure target method is verifiable");

            return false;
        }

        private static Dictionary<Instruction, int> GetInstrToIndexMap(Collection<Instruction> Instructions)
        {
            Dictionary<Instruction, int> instrIndex = new();
            int index = 0;
            foreach (var instr in Instructions)
            {
                instrIndex[instr] = index++;
            }

            return instrIndex;
        }

        private static int ComputeVarpop(Instruction instr)
        {
            if (instr.Operand == null)
            {
                if (instr.OpCode == OpCodes.Ret)
                    return 0;
                throw new NotImplementedException();
            }

            return instr.Operand switch
            {
                MethodReference method => -(method.Parameters.Count + (method.HasThis ? 1 : 0)),
                _ => throw new NotImplementedException(instr.Operand.GetType().FullName),
            };
        }

        private static int ComputeVarpush(Instruction instr)
        {
            if (instr.Operand == null)
            {
                throw new NotImplementedException();
            }

            return instr.Operand switch
            {
                MethodReference method => method.ReturnType.FullName == typeof(void).FullName ? 0 : 1,
                _ => throw new NotImplementedException(instr.Operand.GetType().FullName),
            };
        }

        private static StackState ComputeNewStack(StackState prev, Instruction instr)
        {
            // Console.WriteLine(instr.OpCode);
            var popOff = instr.OpCode.StackBehaviourPop switch
            {
                StackBehaviour.Popi => -1,
                StackBehaviour.Popref => -1,
                StackBehaviour.Pop1 => -1,
                StackBehaviour.Pop1_pop1 => -2,
                StackBehaviour.Popi_pop1 => -2,
                StackBehaviour.Popi_popi => -2,
                StackBehaviour.Popi_popi8 => -2,
                StackBehaviour.Popi_popr4 => -2,
                StackBehaviour.Popi_popr8 => -2,
                StackBehaviour.Popref_pop1 => -2,
                StackBehaviour.Popref_popi => -2,
                StackBehaviour.Popi_popi_popi => -3,
                StackBehaviour.Popref_popi_popi => -3,
                StackBehaviour.Popref_popi_popi8 => -3,
                StackBehaviour.Popref_popi_popr4 => -3,
                StackBehaviour.Popref_popi_popr8 => -3,
                StackBehaviour.Popref_popi_popref => -3,
                StackBehaviour.PopAll => -prev.Count,
                StackBehaviour.Pop0 => 0,
                StackBehaviour.Varpop => ComputeVarpop(instr),
                _ => throw new NotImplementedException(instr.OpCode.StackBehaviourPop.ToString()),

            };

            var pushOff = instr.OpCode.StackBehaviourPush switch
            {
                StackBehaviour.Push1 => 1,
                StackBehaviour.Pushi => 1,
                StackBehaviour.Pushi8 => 1,
                StackBehaviour.Pushr4 => 1,
                StackBehaviour.Pushr8 => 1,
                StackBehaviour.Pushref => 1,
                StackBehaviour.Push1_push1 => 2,
                StackBehaviour.Push0 => 0,
                StackBehaviour.Varpush => ComputeVarpush(instr),
                _ => throw new NotImplementedException(instr.OpCode.StackBehaviourPush.ToString()),
            };

            // Console.WriteLine($"offsets: {popOff} {pushOff}");

            return new StackState(prev.Count + popOff + pushOff);
        }

        private void OnInstrGoto(int targetIndex, StackState currentState)
        {
            if (targetIndex >= State.Length)
                return;
            if (TrySet(targetIndex, currentState))
                InstrsToProcess.Enqueue(new(targetIndex, currentState));
        }

        private StackStateCalculator(Collection<Instruction> Instructions)
        {
            Dictionary<Instruction, int> instrIndexMap = GetInstrToIndexMap(Instructions);

            State = new StackState[Instructions.Count];
            State[0] = new StackState(0);
            InstrsToProcess = new();

            InstrsToProcess.Enqueue(new(0, new StackState(0)));

            while (InstrsToProcess.Count != 0)
            {
                var sz = InstrsToProcess.Count;
                for (int i = 0; i < sz; i++)
                {
                    // Console.WriteLine(string.Join(", ", State.Select(u => u == null ? "?" : u.Count.ToString())));

                    var (instrIndex, prevState) = InstrsToProcess.Dequeue();
                    var instruction = Instructions[instrIndex];
                    var newState = ComputeNewStack(prevState, instruction);

                    if (instruction.OpCode.FlowControl == FlowControl.Branch)
                    {
                        OnInstrGoto(instrIndexMap[((ILLabel)instruction.Operand).Target!], newState);
                    }
                    else if (instruction.OpCode.FlowControl == FlowControl.Cond_Branch)
                    {
                        OnInstrGoto(instrIndexMap[((ILLabel)instruction.Operand).Target!], newState);
                        OnInstrGoto(instrIndex + 1, newState);
                    }
                    else if (instruction.OpCode.FlowControl == FlowControl.Return)
                    {
                        if (newState.Count > 1)
                            throw new MixinProcessorException("return stack is invalid");
                    }
                    else
                    {
                        OnInstrGoto(instrIndex + 1, newState);
                    }
                }
            }

        }

        public static StackState?[] ComputeStackState(Collection<Instruction> instructions)
        {
            return new StackStateCalculator(instructions).State!;
        }
    }
}