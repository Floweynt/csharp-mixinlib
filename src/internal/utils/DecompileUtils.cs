using System.Reflection;
using MixinLib.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MixinLib.Internal
{
    // operands
    public class DecompileOperand
    {
        public virtual void ForeachLabel(Action<int> callback) { }
    };

    public class BrOperand : DecompileOperand
    {
        public string Label { get; private set; }
        public int? Offset { get; private set; }

        public BrOperand(int offset)
        {
            Label = $"[0x{offset:X}]";
            Offset = offset;
        }

        public BrOperand(Instruction? target, Dictionary<Instruction, int>? names = null)
        {
            if (names == null)
            {
                if (target != null)
                {
                    Offset = target.Offset;
                    Label = $"[0x{Offset:X}]";
                }
                Label = "<unk>";
            }
            else
                Label = $".label_{(target != null ? names[target!] : "<unk>")}";
        }

        public override string ToString() => Label;
        public override void ForeachLabel(Action<int> callback)
        {
            if (Offset.HasValue)
                callback(Offset.GetValueOrDefault());
        }
    }

    public class PrimitiveOperand<T> : DecompileOperand where T : notnull
    {
        public T Value { get; private set; }
        public PrimitiveOperand(T value) { Value = value; }
        public override string ToString() => Value.ToString();
    }

    public class StringOperand : PrimitiveOperand<string>
    {
        public StringOperand(string value) : base(value) { }
        public override string ToString() => '"' + Value.ToString() + '"';
    }

    public class LocalVariableOperand : DecompileOperand
    {
        public DecompileLocalVariable Info { get; private set; }
        public LocalVariableOperand(DecompileLocalVariable info) { Info = info; }
        public override string ToString()
            => $"Local({Info.LocalIndex}: {Info.LocalType}{(Info.IsPinned ? " pinned" : "")})";
    }

    public class ParameterOperand : DecompileOperand
    {
        public int Index { get; private set; }
        public bool IsThis { get; private set; }
        public DecompileMethodParameter? Info { get; private set; }

        public ParameterOperand(int index, DecompiledMethod? method = null)
        {
            IsThis = false;
            Index = index;

            if (method != null)
            {
                if (method.IsStatic)
                    Info = method.Parameters[index];
                else
                {
                    if (index == 0)
                        IsThis = true;
                    else
                        Info = method.Parameters[index - 1];
                }
            }
        }

        public override string ToString()
        {
            if (!IsThis && Info == null)
                return $"Param({Index})";
            return $"Param( {Info?.GetType().FullName ?? "this"})";
        }
    }

    public class SwitchOperand : DecompileOperand
    {
        public BrOperand[] Targets { get; private set; }

        public SwitchOperand(int[] targets)
        {
            Targets = targets.Select(u => new BrOperand(u)).ToArray();
        }

        public override string ToString() => $"{string.Join<BrOperand>(", ", Targets)}";
        public override void ForeachLabel(Action<int> callback)
        {
            foreach (var item in Targets)
            {
                callback(item.Offset.GetValueOrDefault());
            }
        }
    }

    public class TypeOperand : DecompileOperand
    {
        public TypeDescriptor Info { get; private set; }
        public TypeOperand(Type info) { Info = new TypeDescriptor(info); }
        public TypeOperand(TypeReference info) { Info = new TypeDescriptor(info.FullName); }
        public override string ToString() => Info.ToString();
    }

    public class MethodOperand : DecompileOperand
    {
        public TypeDescriptor? ReturnType { get; private set; }
        public TypeDescriptor DeclaringType { get; private set; }
        public string Name { get; private set; }
        public TypeDescriptor[] Parameters { get; private set; }

        public MethodOperand(MethodBase info)
        {
            ReturnType = info is MethodInfo method ? new TypeDescriptor(method.ReturnType) : null;
            DeclaringType = new TypeDescriptor(info.DeclaringType);
            Name = info.Name;
            Parameters = info.GetParameters().Select(u => new TypeDescriptor(u.ParameterType)).ToArray();
        }

        public MethodOperand(MethodReference info)
        {
            ReturnType = new TypeDescriptor(info.ReturnType.FullName);
            DeclaringType = new TypeDescriptor(info.DeclaringType == null ? "<anon>" : info.DeclaringType.FullName);
            Name = info.Name;
            Parameters = info.Parameters.Select(u => new TypeDescriptor(u.ParameterType.FullName)).ToArray();
        }

        public override string ToString()
        {
            var param = string.Join(", ", Parameters);

            return ReturnType != null ?
                $"{ReturnType} {DeclaringType}::{Name}({param})" :
                $"{DeclaringType}::{Name}({param})";
        }
    }

    public class FieldOperand : DecompileOperand
    {
        public TypeDescriptor DeclaringType { get; private set; }
        public string Name { get; private set; }

        public FieldOperand(FieldInfo info)
        {
            DeclaringType = new TypeDescriptor(info.DeclaringType);
            Name = info.Name;
        }

        public FieldOperand(FieldReference info)
        {
            DeclaringType = new TypeDescriptor(info.DeclaringType.FullName);
            Name = info.Name;
        }

        public override string ToString() => $"{DeclaringType}::{Name}";
    }

    public class DecompileInstr : IFormattable
    {
        public DecompileInstr(OpCode opcode, int offset, DecompileOperand? operand = null)
        {
            Opcode = opcode;
            Operand = operand;
            Offset = offset;
        }

        public DecompileInstr(Instruction cecilInstr, Dictionary<Instruction, int>? names = null)
        {
            Opcode = cecilInstr.OpCode;
            Offset = cecilInstr.Offset;
            if (names != null && names.ContainsKey(cecilInstr))
                Label = $".label_{names[cecilInstr]}";

            Operand = cecilInstr.Operand switch
            {
                DynamicMethodReference i => new MethodOperand(i.DynamicMethod),
                MethodReference i => new MethodOperand(i),
                FieldReference i => new FieldOperand(i),
                MethodBase i => new MethodOperand(i),
                TypeReference i => new TypeOperand(i),
                ParameterReference i => new ParameterOperand(i.Index),
                VariableDefinition i => new LocalVariableOperand(new DecompileLocalVariable(i)),
                ILLabel i => new BrOperand(i.Target, names),
                Instruction i => new BrOperand(i, names),
                byte[] i => new PrimitiveOperand<byte[]>(i),
                int[] i => new SwitchOperand(i),
                string i => new StringOperand(i),
                int i => new PrimitiveOperand<int>(i),
                long i => new PrimitiveOperand<long>(i),
                float i => new PrimitiveOperand<float>(i),
                double i => new PrimitiveOperand<double>(i),
                null => null,
                _ => throw new NotImplementedException($"not implemented type: {cecilInstr.Operand.GetType()}, instr = {cecilInstr.OpCode}")
            };
        }

        public readonly int Offset;
        public readonly OpCode Opcode;
        public DecompileOperand? Operand;
        public bool HasJumpTarget;
        public string? Label;

        public override string ToString()
        {
            return ToString("[$offset]$label $opc $operand");
        }

        public string ToString(string format, IFormatProvider? formatProvider = null)
        {
            if(format == null)
                return ToString();

            var labelText = Label != null ? $" {Label}:" : "";
            var offsetText = $"0x{Offset:X}";

            return format
                .Replace("$offset", offsetText)
                .Replace("$label", labelText)
                .Replace("$opc", Opcode.ToString())
                .Replace("$operand", Operand?.ToString() ?? "");
        }
    }

    public class DecompileLocalVariable
    {
        public DecompileLocalVariable(LocalVariableInfo info)
        {
            LocalType = new TypeDescriptor(info.LocalType);
            IsPinned = info.IsPinned;
            LocalIndex = info.LocalIndex;
        }

        public DecompileLocalVariable(VariableDefinition info)
        {
            LocalType = new TypeDescriptor(info.VariableType.FullName);
            IsPinned = info.IsPinned;
            LocalIndex = info.Index;
        }

        public TypeDescriptor LocalType { get; private set; }
        public bool IsPinned { get; private set; }
        public int LocalIndex { get; private set; }
    }

    public class DecompileMethodParameter
    {
        public DecompileMethodParameter(ParameterInfo info)
        {
            Type = new TypeDescriptor(info.ParameterType);
        }

        public DecompileMethodParameter(ParameterDefinition info)
        {
            Type = new TypeDescriptor(info.ParameterType.FullName);
        }

        public TypeDescriptor Type { get; private set; }
    }

    public class DecompiledMethod
    {
        // setup stuff
        private static readonly OpCode[] CecilOpCodes1;
        private static readonly OpCode[] CecilOpcodes2;

        static DecompiledMethod()
        {
            CecilOpCodes1 = new OpCode[0xe1];
            CecilOpcodes2 = new OpCode[0x1f];

            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                OpCode opcode = (OpCode)field.GetValue(null);
                if (opcode.OpCodeType == OpCodeType.Nternal)
                    continue;
                if (opcode.Size == 1)
                    CecilOpCodes1[opcode.Value] = opcode;
                else
                    CecilOpcodes2[opcode.Value & 0xff] = opcode;
            }
        }

        private enum TokenResolutionMode
        {
            Any,
            Type,
            Method,
            Field
        }

        private DecompileOperand? ResolveTokenAs(int token, TokenResolutionMode resolveMode, Module moduleFrom)
        {
            try
            {
                return resolveMode switch
                {
                    TokenResolutionMode.Type => new TypeOperand(moduleFrom.ResolveType(token, _TypeArguments, _MethodTypeArguments)),
                    TokenResolutionMode.Method => new MethodOperand(moduleFrom.ResolveMethod(token, _TypeArguments, _MethodTypeArguments)),
                    TokenResolutionMode.Field => new FieldOperand(moduleFrom.ResolveField(token, _TypeArguments, _MethodTypeArguments)),
                    TokenResolutionMode.Any => moduleFrom.ResolveMember(token, _TypeArguments, _MethodTypeArguments) switch
                    {
                        Type i => new TypeOperand(i),
                        MethodBase i => new MethodOperand(i),
                        FieldInfo i => new FieldOperand(i),
                        var resolved => throw new NotSupportedException($"Invalid resolved member type {resolved.GetType()}"),
                    },
                    _ => throw new NotSupportedException($"Invalid TokenResolutionMode {resolveMode}"),
                };
            }
            catch (MissingMemberException)
            {
                return null;
            }
        }

        private DecompileOperand ReadSwitch(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var offs = (int)reader.BaseStream.Position + (4 * length);
            int[] targets = new int[length];
            for (int i = 0; i < length; i++)
                targets[i] = reader.ReadInt32() + offs;
            return new SwitchOperand(targets);
        }

        private DecompileOperand? ReadOperand(BinaryReader reader, DecompileInstr instr, Module moduleFrom)
        {
            return instr.Opcode.OperandType switch
            {
                OperandType.InlineNone => null,
                OperandType.InlineSwitch => ReadSwitch(reader),
                // this is important: read then add
                OperandType.ShortInlineBrTarget => new BrOperand(reader.ReadSByte() + (int)reader.BaseStream.Position),
                OperandType.InlineBrTarget => new BrOperand(reader.ReadInt32() + (int)reader.BaseStream.Position),
                OperandType.ShortInlineI => new PrimitiveOperand<int>(instr.Opcode == OpCodes.Ldc_I4_S ? reader.ReadSByte() : reader.ReadByte()),
                OperandType.InlineI => new PrimitiveOperand<int>(reader.ReadInt32()),
                OperandType.ShortInlineR => new PrimitiveOperand<float>(reader.ReadSingle()),
                OperandType.InlineR => new PrimitiveOperand<double>(reader.ReadDouble()),
                OperandType.InlineI8 => new PrimitiveOperand<long>(reader.ReadInt64()),
                OperandType.InlineSig => new PrimitiveOperand<byte[]>(moduleFrom.ResolveSignature(reader.ReadInt32())),
                OperandType.InlineString => new StringOperand(moduleFrom.ResolveString(reader.ReadInt32())),
                OperandType.InlineTok => ResolveTokenAs(reader.ReadInt32(), TokenResolutionMode.Any, moduleFrom),
                OperandType.InlineType => ResolveTokenAs(reader.ReadInt32(), TokenResolutionMode.Type, moduleFrom),
                OperandType.InlineMethod => ResolveTokenAs(reader.ReadInt32(), TokenResolutionMode.Method, moduleFrom),
                OperandType.InlineField => ResolveTokenAs(reader.ReadInt32(), TokenResolutionMode.Field, moduleFrom),
                OperandType.ShortInlineVar or OperandType.InlineVar =>
                    new LocalVariableOperand(Locals[instr.Opcode.OperandType == OperandType.ShortInlineVar ? reader.ReadByte() : reader.ReadInt16()]),
                OperandType.InlineArg or OperandType.ShortInlineArg
                    => new ParameterOperand(instr.Opcode.OperandType == OperandType.ShortInlineArg ? reader.ReadByte() : reader.ReadInt16(), this),
                _ => throw new NotSupportedException($"Unsupported opcode ${instr.Opcode.Name}"),
            };
        }

        // fields
        public List<DecompileInstr> Instructions { get; private set; }
        public bool IsStatic { get; private set; }
        public MethodBase? Method { get; private set; }
        public TypeDescriptor[]? TypeArguments { get; private set; }
        public TypeDescriptor[]? MethodTypeArguments { get; private set; }

        private readonly Type[]? _TypeArguments;
        private readonly Type[]? _MethodTypeArguments;

        public DecompileLocalVariable[] Locals { get; private set; }
        public DecompileMethodParameter[] Parameters { get; private set; }

        private DecompiledMethod(MethodBase method)
        {
            Method = method;
            Locals = Method.GetMethodBody().LocalVariables.Select(u => new DecompileLocalVariable(u)).ToArray();
            Parameters = Method.GetParameters().Select(u => new DecompileMethodParameter(u)).ToArray();
            Instructions = new();

            IsStatic = Method.IsStatic;

            if (method.DeclaringType.IsGenericType)
            {
                _TypeArguments = method.DeclaringType.GetGenericArguments();
                TypeArguments = _TypeArguments.Select(u => new TypeDescriptor(u)).ToArray();
            }
            if (method.IsGenericMethod)
            {
                _MethodTypeArguments = method.GetGenericArguments();
                MethodTypeArguments = _MethodTypeArguments.Select(u => new TypeDescriptor(u)).ToArray();
            }

            var bodyFrom = method.GetMethodBody();
            var data = (bodyFrom?.GetILAsByteArray()) ?? throw new NotSupportedException("Body-less method");

            using BinaryReader reader = new(new MemoryStream(data));

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                int offset = (int)reader.BaseStream.Position;
                byte op = reader.ReadByte();
                var instr = new DecompileInstr(op != 0xfe ? CecilOpCodes1[op] : CecilOpcodes2[reader.ReadByte()], offset);

                instr.Operand = ReadOperand(reader, instr, Method.Module);
                Instructions.Add(instr);
            }

            foreach (var instr in Instructions)
            {
                instr.Operand?.ForeachLabel(u =>
                {
                    var target = GetInstruction(u);
                    if (target != null)
                        target.HasJumpTarget = true;
                });
            }
        }

        private DecompiledMethod(ILContext context)
        {
            Method = null;
            Locals = context.Body.Variables.Select(u => new DecompileLocalVariable(u)).ToArray();
            Parameters = context.Method.Parameters.Select(u => new DecompileMethodParameter(u)).ToArray();
            Instructions = new();
            IsStatic = context.Method.IsStatic;

            if (context.Method.DeclaringType.IsGenericInstance)
                TypeArguments = context.Method.DeclaringType.GenericParameters.Select(u => new TypeDescriptor(u.FullName)).ToArray();
            if (context.Method.IsGenericInstance)
                MethodTypeArguments = context.Method.GenericParameters.Select(u => new TypeDescriptor(u.FullName)).ToArray();

            Dictionary<Instruction, int> labels = new();
            int currId = 0;

            foreach (var cecilInstr in context.Body.Instructions)
            {
                if (cecilInstr.Operand is ILLabel label && label.Target != null)
                {
                    if (!labels.ContainsKey(label.Target))
                        labels.Add(label.Target, currId++);
                }
                else if (cecilInstr.Operand is Instruction instr)
                {
                    if (!labels.ContainsKey(instr))
                        labels.Add(instr, currId++);
                }
            }

            foreach (var cecilInstr in context.Body.Instructions)
            {
                Instructions.Add(new DecompileInstr(cecilInstr, labels));
            }

            foreach (var instr in Instructions)
            {
                instr.Operand?.ForeachLabel(u =>
                {
                    var target = GetInstruction(u);
                    if (target != null)
                        target.HasJumpTarget = true;
                });
            }
        }

        public class Decompiler
        {
            private readonly Cache<MethodBase, DecompiledMethod> MethodDecompCache = new();
            private readonly Cache<ILContext, DecompiledMethod> CecilTranslateCache = new();
            private readonly MixinContext Context;

            public Decompiler(MixinContext context)
            {
                Context = context;
            }

            public DecompiledMethod Decompile(MethodBase method, bool force = false)
            {
                Context.Logger.Trace("decompiling method, this is a slow code path");
                return MethodDecompCache.Get(method, m => new DecompiledMethod(m), force);
            }

            public DecompiledMethod FromMonomod(ILContext context, bool force = false)
            {
                Context.Logger.Trace("copying this is also a slow code path, this should be avoided");
                return CecilTranslateCache.Get(context, m => new DecompiledMethod(m), force);
            }
        }

        public DecompileInstr? GetInstruction(int offset)
        {
            int last = Instructions.Count - 1;
            if (offset < 0 || offset > Instructions[last].Offset)
                return null;

            int min = 0;
            int max = last;
            while (min <= max)
            {
                int mid = min + ((max - min) / 2);
                DecompileInstr instr = Instructions[mid];

                if (offset == instr.Offset)
                    return instr;

                if (offset < instr.Offset)
                    max = mid - 1;
                else
                    min = mid + 1;
            }

            return null;
        }
    }
}