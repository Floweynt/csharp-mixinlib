namespace MixinLib.Attributes
{
    // tells an injector where to look for stuff
    // these are different variants of the "at" injector
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public abstract class At : Attribute
    {
        public static int ShiftAfter = 1;
        public static int ShiftBefore = 1;

        public int Shift = 0;
        public int Ordinal = -1;

        public string? Id;
    }

    // selects the first instruction
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtHead : At
    {
        public AtHead() { }
    }

    // selects the last instruction
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtTail : At
    {
        public AtTail() { }
    }

    // selects return instruction
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtReturn : At
    {
        public AtReturn() { }
    }

    // selects a method call instruction
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtInvoke : At
    {
        public readonly TypeDescriptor TargetType;
        public readonly string Name;
        public readonly TypeDescriptor[]? Parameters;

        [Flags]
        public enum FunctionType
        {
            Static = 1,
            Member = 2,
        };

        public FunctionType Staticness = FunctionType.Static | FunctionType.Member;

        public AtInvoke(Type type, string name)
        {
            TargetType = new TypeDescriptor(type);
            Name = name;
        }

        public AtInvoke(Type type, string name, params Type[] args)
        {
            TargetType = new TypeDescriptor(type);
            Name = name;
            Parameters = Array.ConvertAll(args, item => new TypeDescriptor(item));
        }

        public AtInvoke(string type, string name, params string[] args)
        {
            TargetType = new TypeDescriptor(type);
            Name = name;
            Parameters = Array.ConvertAll(args, item => new TypeDescriptor(item));
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class AtLoadConst : At
    {
        public readonly object Value;
        public AtLoadConst(int value) { Value = value; }
        public AtLoadConst(long value) { Value = value; }
        public AtLoadConst(float value) { Value = value; }
        public AtLoadConst(double value) { Value = value; }
        public AtLoadConst(string value) { Value = value; }
        public AtLoadConst(bool value) { Value = value ? 1 : 0; }
    }
}