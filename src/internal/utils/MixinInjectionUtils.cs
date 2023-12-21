using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MixinLib.Internal
{
    enum CursorLocation
    {
        // <- *inject here*
        // label:
        // instr
        BEFORE_LABEL,

        // label:
        // <- *inject here*
        // instr
        BEFORE,

        ON_INSTR,

        // instr
        // <- *inject here*
        // label:
        AFTER,

        // instr
        // label:
        // <- *inject here*
        AFTER_LABEL
    }

    class InstructionCursor
    {
        public int TargetIndex { get; private set; }
        public Instruction Instr => Context.Instrs[TargetIndex];
        public readonly ILContext Context;

        public InstructionCursor(ILContext context)
        {
            TargetIndex = 0;
            Context = context;
        }

        private void RemapLabels(Instruction oldTarget, Instruction newTarget)
        {
            foreach (var label in Context.GetIncomingLabels(oldTarget))
            {
                label.Target = newTarget;
            }
        }

        public void Inject(CursorLocation location, IEnumerable<Instruction> instrs, bool advance = true)
        {
            var currInstrs = Context.Instrs;
            if (location < CursorLocation.ON_INSTR)
            {
                var newTarget = instrs.First();
                var oldTarget = Instr;
                currInstrs.InsertRange(TargetIndex, instrs);
                if (location == CursorLocation.BEFORE)
                {
                    RemapLabels(oldTarget, newTarget);
                }
            }
            else if (location > CursorLocation.ON_INSTR)
            {
                var newTarget = instrs.First();
                var oldTarget = TargetIndex + 1 < currInstrs.Count ? currInstrs[TargetIndex + 1] : null;

                currInstrs.InsertRange(TargetIndex + 1, instrs);
                if (location == CursorLocation.BEFORE && oldTarget != null)
                {
                    RemapLabels(oldTarget, newTarget);
                }
            }
            else
            {
                var newTarget = instrs.First();
                var oldTarget = Instr;
                currInstrs.RemoveAt(TargetIndex);
                currInstrs.InsertRange(TargetIndex, instrs);
                RemapLabels(oldTarget, newTarget);
            }

            if (advance)
            {
                TargetIndex += instrs.Count() + 1;
                if (location == CursorLocation.ON_INSTR)
                    TargetIndex--;
            }
        }

        public void Inject(CursorLocation location, IEnumerable<ILBlock> blocks, bool advance = true)
            => Inject(location, blocks.SelectMany(instr => instr.Instrs), advance);
        public void Inject(CursorLocation location, ILBlock block, bool advance = true)
            => Inject(location, block.Instrs, advance);

        public InstructionCursor Next()
        {
            TargetIndex++;
            return this;
        }
    }

    // collection of instructions to insert
    public class ILBlock
    {
        private delegate Instruction InstrProducer();

        private ILBlock DoEmit(InstrProducer producer, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                var instrToAdd = producer();
                instrToAdd.Offset = Tag;
                Instrs.Add(instrToAdd);
            }
            return this;
        }

        public readonly List<Instruction> Instrs;
        private readonly ILContext Context;
        public readonly int Tag;

        public ILBlock(ILContext context, int tag = -1)
        {
            Context = context;
            Instrs = new();
            Tag = tag;
        }

        public Instruction? GetStartingInstruction()
        {
            return Instrs.FirstOrDefault(null);
        }

        public ILBlock Emit(Instruction instr) => DoEmit(() => instr);

        public ILBlock Emit(OpCode opcode, ParameterDefinition parameter) => DoEmit(() => Context.IL.Create(opcode, parameter));
        public ILBlock Emit(OpCode opcode, VariableDefinition variable) => DoEmit(() => Context.IL.Create(opcode, variable));
        public ILBlock Emit(OpCode opcode, Instruction[] targets) => DoEmit(() => Context.IL.Create(opcode, targets));
        public ILBlock Emit(OpCode opcode, Instruction target) => DoEmit(() => Context.IL.Create(opcode, target));
        public ILBlock Emit(OpCode opcode, double value) => DoEmit(() => Context.IL.Create(opcode, value));
        public ILBlock Emit(OpCode opcode, float value) => DoEmit(() => Context.IL.Create(opcode, value));
        public ILBlock Emit(OpCode opcode, long value) => DoEmit(() => Context.IL.Create(opcode, value));
        public ILBlock Emit(OpCode opcode, byte value) => DoEmit(() => Context.IL.Create(opcode, value));
        public ILBlock Emit(OpCode opcode, sbyte value) => DoEmit(() => Context.IL.Create(opcode, value));
        public ILBlock Emit(OpCode opcode, string value) => DoEmit(() => Context.IL.Create(opcode, value));
        public ILBlock Emit(OpCode opcode, FieldReference field) => DoEmit(() => Context.IL.Create(opcode, field));
        public ILBlock Emit(OpCode opcode, MethodReference method) => DoEmit(() => Context.IL.Create(opcode, method));
        public ILBlock Emit(OpCode opcode, CallSite site) => DoEmit(() => Context.IL.Create(opcode, site));
        public ILBlock Emit(OpCode opcode, TypeReference type) => DoEmit(() => Context.IL.Create(opcode, type));
        public ILBlock Emit(OpCode opcode) => DoEmit(() => Context.IL.Create(opcode));
        public ILBlock Emit(OpCode opcode, int value) => DoEmit(() => Context.IL.Create(opcode, value));
        public ILBlock Emit(OpCode opcode, MethodBase method) => DoEmit(() => Context.IL.Create(opcode, method));
        public ILBlock Emit(OpCode opcode, FieldInfo field) => DoEmit(() => Context.IL.Create(opcode, field));
        public ILBlock Emit(OpCode opcode, MemberInfo member) => DoEmit(() => Context.IL.Create(opcode, member));
        public ILBlock Emit(OpCode opcode, Type type) => DoEmit(() => Context.IL.Create(opcode, type));

        public ILBlock Emit(int count, OpCode opcode, ParameterDefinition parameter) => DoEmit(() => Context.IL.Create(opcode, parameter), count);
        public ILBlock Emit(int count, OpCode opcode, VariableDefinition variable) => DoEmit(() => Context.IL.Create(opcode, variable), count);
        public ILBlock Emit(int count, OpCode opcode, Instruction[] targets) => DoEmit(() => Context.IL.Create(opcode, targets), count);
        public ILBlock Emit(int count, OpCode opcode, Instruction target) => DoEmit(() => Context.IL.Create(opcode, target), count);
        public ILBlock Emit(int count, OpCode opcode, double value) => DoEmit(() => Context.IL.Create(opcode, value), count);
        public ILBlock Emit(int count, OpCode opcode, float value) => DoEmit(() => Context.IL.Create(opcode, value), count);
        public ILBlock Emit(int count, OpCode opcode, long value) => DoEmit(() => Context.IL.Create(opcode, value), count);
        public ILBlock Emit(int count, OpCode opcode, byte value) => DoEmit(() => Context.IL.Create(opcode, value), count);
        public ILBlock Emit(int count, OpCode opcode, sbyte value) => DoEmit(() => Context.IL.Create(opcode, value), count);
        public ILBlock Emit(int count, OpCode opcode, string value) => DoEmit(() => Context.IL.Create(opcode, value), count);
        public ILBlock Emit(int count, OpCode opcode, FieldReference field) => DoEmit(() => Context.IL.Create(opcode, field), count);
        public ILBlock Emit(int count, OpCode opcode, MethodReference method) => DoEmit(() => Context.IL.Create(opcode, method), count);
        public ILBlock Emit(int count, OpCode opcode, CallSite site) => DoEmit(() => Context.IL.Create(opcode, site), count);
        public ILBlock Emit(int count, OpCode opcode, TypeReference type) => DoEmit(() => Context.IL.Create(opcode, type), count);
        public ILBlock Emit(int count, OpCode opcode) => DoEmit(() => Context.IL.Create(opcode), count);
        public ILBlock Emit(int count, OpCode opcode, int value) => DoEmit(() => Context.IL.Create(opcode, value), count);

        public ILBlock Emit(int count, OpCode opcode, MethodBase method) => DoEmit(() => Context.IL.Create(opcode, method), count);
        public ILBlock Emit(int count, OpCode opcode, FieldInfo field) => DoEmit(() => Context.IL.Create(opcode, field), count);
        public ILBlock Emit(int count, OpCode opcode, MemberInfo member) => DoEmit(() => Context.IL.Create(opcode, member), count);
        public ILBlock Emit(int count, OpCode opcode, Type type) => DoEmit(() => Context.IL.Create(opcode, type), count);
    }

    public class ILBlockContainer
    {
        public readonly List<object> Instrs = new();
        public ILBlockContainer() { }

        public ILBlockContainer Add(ILBlock block)
        {
            Instrs.Add(block);
            return this;
        }

        public ILBlockContainer Add(IEnumerable<ILBlock> blocks)
        {
            Instrs.AddRange(blocks);
            return this;
        }

        public ILBlockContainer Add(Instruction instr)
        {
            Instrs.Add(instr);
            return this;
        }

        public ILBlockContainer Add(IEnumerable<Instruction> instrs)
        {
            Instrs.AddRange(instrs);
            return this;
        }

        public IEnumerable<Instruction> Enumerable()
        {
            return Instrs.SelectMany(u => u switch
            {
                Instruction i => System.Linq.Enumerable.Repeat(i, 1),
                ILBlock i => i.Instrs,
                _ => throw new NotImplementedException()
            });
        }
    }
}