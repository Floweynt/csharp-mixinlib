namespace MixinLib.Attributes
{
    // injectors
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public abstract class Injector : Attribute
    {
        // special flag value
        public int Priority = int.MinValue;
        public bool Disable = false;
        public string? Id;
        public bool Required = false;
        public int MaxInjections = -1;
        public int MinInjections = 1;
    }

    // how local vars should be captured
    public enum LocalCaptureMode
    {
        FAILEXCEPTION,
        FAILHARD,
        FAILSOFT,
        DONT
    };

    // a "trivial" injector
    // it doesn't do anything too fancy
    // <injected code that calls the annotated method>
    // [targetinstr]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class Inject : Injector
    {
        public bool Cancellable = false;
        public LocalCaptureMode Locals = LocalCaptureMode.DONT;
        public Inject() { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    sealed public class ModifyConstant : Injector
    {
        public LocalCaptureMode Locals = LocalCaptureMode.DONT;
        public ModifyConstant() { }
    }
}