namespace MixinLib.Attributes
{
    /// <summary>
    /// The base attribute for all selectors. This class should never be used directly in a mixin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public abstract class At : Attribute
    {
        public static int ShiftAfter = 1;
        public static int ShiftBefore = 1;

        /// <summary>
        /// How much to shift the matched instruction by.
        /// If the selector matches instruction <c>i</c>-th instruction, it instead matches the <c>i + Shift</c>-th instruction
        /// </summary>
        public int Shift = 0;

        /// <summary>
        /// The nth matched instruction to select. By default, the value of -1 selects all matching instructions
        /// </summary>
        public int Ordinal = -1;

        public string? Id;
    }

    /// <summary>
    /// Selects the first instruction in a method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtHead : At
    {
        public AtHead() { }
    }

    /// <summary>
    /// Selects the last instruction in a method. This may or may not correspond to the last ret statement
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtTail : At
    {
        public AtTail() { }
    }

    /// <summary>
    /// Selects all <c>ret</c> instruction
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtReturn : At
    {
        public AtReturn() { }
    }

    /// <summary>
    /// Selects <c>call/callvirt</c> instruction, with specific parameters
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtInvoke : At
    {
        public readonly TypeDescriptor TargetType;
        public readonly string Name;
        public readonly TypeDescriptor[]? Parameters;

        /// <summary>
        /// Allowed call types, can be either <c>call</c> or <c>callvirt</c>
        /// </summary>
        [Flags]
        public enum CallInstrType
        {
            CALL = 1,
            CALLVIRT = 2,
        };

        /// <summary>
        /// Allowed call types
        /// </summary>
        public CallInstrType CallType = CallInstrType.CALL | CallInstrType.CALLVIRT;

        /// <summary>
        /// Match calls in <c>type</c> with <c>name</c>.
        /// All overloads are matched
        /// </summary>
        public AtInvoke(Type type, string name)
        {
            TargetType = new TypeDescriptor(type);
            Name = name;
        }

        /// <summary>
        /// Match calls in <c>type</c> with <c>name</c>.
        /// The overload with types matching <c>args</c> are matched
        /// </summary>
        public AtInvoke(Type type, string name, params Type[] args)
        {
            TargetType = new TypeDescriptor(type);
            Name = name;
            Parameters = Array.ConvertAll(args, item => new TypeDescriptor(item));
        }

        /// <summary>
        /// Match calls in <c>type</c> with <c>name</c>.
        /// The overload with types matching <c>args</c> are matched
        /// </summary>
        public AtInvoke(string type, string name, params string[] args)
        {
            TargetType = new TypeDescriptor(type);
            Name = name;
            Parameters = Array.ConvertAll(args, item => new TypeDescriptor(item));
        }
    }

    /// <summary>
    /// Selects <c>ldc*</c> and <c>ldstr</c> instruction, with specific parameters
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtLoadConst : At
    {
        /// <summary>
        /// The value loaded
        /// </summary>
        public readonly object Value;

        /// <summary>
        /// Selects <c>ldc.i4</c> with the specified value
        /// </summary>
        public AtLoadConst(int value) { Value = value; }

        /// <summary>
        /// Selects <c>ldc.i8</c> with the specified value
        /// </summary>
        public AtLoadConst(long value) { Value = value; }

        /// <summary>
        /// Selects <c>ldc.r4</c> with the specified value
        /// </summary>
        public AtLoadConst(float value) { Value = value; }

        /// <summary>
        /// Selects <c>ldc.r8</c> with the specified value
        /// </summary>
        public AtLoadConst(double value) { Value = value; }

        /// <summary>
        /// Selects <c>ldstr</c> with the specified value
        /// </summary>
        public AtLoadConst(string value) { Value = value; }

        /// <summary>
        /// Selects <c>ldc</c> of either 1 or 0
        /// </summary>
        public AtLoadConst(bool value) { Value = value ? 1 : 0; }
    }
}