namespace MixinLib.Attributes
{
    /// <summary>
    /// A class that represents a reference to a "type."
    /// This can simply be a Type, or the name of the type
    /// </summary>
    public struct TypeDescriptor
    {
        public string? Name;
        public Type? Type;

        public TypeDescriptor(Type Type) { this.Type = Type; }
        public TypeDescriptor(string Name) { this.Name = Name; }
    };

    /// <summary>
    /// Attribute that tells the MixinLib processor to treat the class as a mixin
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    sealed public class Mixin : Attribute
    {
        /// <summary>
        /// List of classes to inject into
        /// </summary>
        public readonly TypeDescriptor[] Target;

        /// <summary>
        /// The default priority of this mixin
        /// </summary>
        public int Priority = 0;

        public Mixin(params Type[] typeTarget)
        {
            Target = typeTarget.Select(u => new TypeDescriptor(u)).ToArray();
        }

        public Mixin(params string[] namedTarget)
        {
            Target = namedTarget.Select(u => new TypeDescriptor(u)).ToArray();
        }
    }

    /// <summary>
    /// How MixinLib should fail if a target/selector does not match
    /// </summary>
    public enum SelectFailureMode
    {
        FAIL_EXCEPTION,
        FAIL_MESSAGE,
        FAIL_SOFT,
    };

    /// <summary>
    /// Tells an injector that it should target a specific method.
    /// This is required for all injectors!
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    sealed public class Target : Attribute
    {
        public readonly string Name;
        public readonly TypeDescriptor[]? Arguments;
        public SelectFailureMode FailMode = SelectFailureMode.FAIL_EXCEPTION;

        public Target(string name)
        {
            Name = name;
        }

        public Target(string name, params Type[] args)
        {
            Name = name;
            Arguments = Array.ConvertAll(args, item => new TypeDescriptor(item));
        }

        public Target(string name, params string[] args)
        {
            Name = name;
            Arguments = Array.ConvertAll(args, item => new TypeDescriptor(item));
        }

        public override string ToString()
        {
            string args = Arguments == null ? "*" : string.Join(",", Arguments);
            return $"[Target(\"{Name}\", Arguments = [{args}], FailMode = {FailMode})]";
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class Shadow : Attribute { };
}