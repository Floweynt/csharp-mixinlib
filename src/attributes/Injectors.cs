namespace MixinLib.Attributes
{
    /// <summary>
    /// Parent class of all injectors. Should never be directly used in a mixin
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public abstract class Injector : Attribute
    {
        /// <summary>
        /// Specifies the priority of this injector. If left as is, inherits value from the `[Mixin]` attribute
        /// </summary>
        public int Priority = int.MinValue;

        /// <summary>
        /// Specifies if MixinLib should even consider this method
        /// </summary>
        public bool Disable = false;

        /// <summary>
        /// Name of the injection, useful for debugging
        /// </summary>
        public string? Id;

        /// <summary>
        /// Specifies if MixinLib should crash if it cannot inject this injector
        /// </summary>
        public bool Required = false;


        /// <summary>
        /// The max injections before a warning is raised. By default, there is no limit
        /// </summary>
        public int MaxInjections = -1;

        /// <summary>
        /// The min injections before a warning is raised. By default, MixinLib expects one injection
        /// </summary>
        public int MinInjections = 1;
    }

    /// <summary>
    /// How local vars should be captured. This is not implemented
    /// <summary>
    public enum LocalCaptureMode
    {
        PRINT,
        CAPTURE,
        DONT
    };

    /// <summary>
    /// <para>A "trivial" injector that inserts a call to the injector method body before the instruction. </para>
    /// <para>The injector body must have the signature <c>void (CallbackInfo(Return), capture-args...)</c></para>
    /// <para><cref>CallbackInfo</cref> vs <cref>CallbackInfoReturn</cref> is determined by the return type of the target</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class Inject : Injector
    {

        /// <summary>
        /// Specifies if the injector be allowed to return from the target method
        /// </summary>
        public bool Cancellable = false;

        /// <summary>
        /// Specifies how local variables are captured. This is not implemented
        /// </summary>
        public LocalCaptureMode Locals = LocalCaptureMode.DONT;
        public Inject() { }
    }

    /// <summary>
    /// Modifies a load constant instruction
    /// The injector body must have the signature <c>constant-type (constant-type, capture-args...)</c>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class ModifyConstant : Injector
    {
        /// <summary>
        /// Specifies how local variables are captured. This is not implemented
        /// </summary>
        public LocalCaptureMode Locals = LocalCaptureMode.DONT;
        public ModifyConstant() { }
    }
}