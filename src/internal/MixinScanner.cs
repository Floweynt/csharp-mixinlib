using System.Reflection;
using MixinLib.Attributes;

namespace MixinLib.Internal
{
    // this class should scan for mixins, and provide information about each
#pragma warning disable CS8618
    public class MixinInfo
    {
        public MixinInfo() { }

        // the type of the class annotationed with [Mixin]
        public Type MixinType;

        // the list of targeted classes
        public Type[] TargetClasses;

        // the list of methods annotated with [x extends Injector]
        // these will br processed by the transformer
        public List<InjectorInfo> Injectors;

        // list of all non-shadow methods
        // they will be transformed so that they behave similar to an instance method of a type in TargetClasses
        public List<MethodBase> MethodsToRemap;

        // shadowed methods, similar to shadow fields
        // calls to a shadow-ed method should be translated to a call to the method with the same signature
        // in a specific target class
        public List<MethodBase> ShadowMethods;

        // shadowed fields, similar to shadowed method
        // load/store from a shadow-ed field will be translated to a load/store from the field with the same name
        // in a specific target class
        public List<MethodBase> ShadowFields;
    };

    // information for a specific injector
    public class InjectorInfo
    {
        public InjectorInfo() { }

        // the instance of the injector annotation
        public Injector Injector;

        // the method to be injected
        // note that for remapped methods should be used when actually doing injects
        public MethodBase Body;

        // instruction selectors
        // each selector will return a list of instructions
        public At[] Selectors;

        // methods to target in the target class
        public Target[] Targets;

        public int Priority;

        public int InjectionCount;
    };
#pragma warning restore CS8618

    class MixinScanner
    {
        // context stuff
        private readonly MixinContext Context;

        public MixinScanner(MixinContext context) { Context = context; }

        private static void ValidateInjector(Injector[] injectors, Target[] targets, At[] selectors)
        {
            // each method in the mixin class can only have one injector
            if (injectors.Length > 1)
                throw new MixinProcessorException("Multiple injectors not supported");

            var injector = injectors[0];

            // ban [Injector]
            if (injector.GetType().Equals(typeof(Injector)))
                throw new MixinProcessorException("Direct use of the 'Injector' base is not allowed");

            // scan for selectors
            foreach (At at in selectors)
            {
                if (at.GetType().Equals(typeof(At)))
                    throw new MixinProcessorException("Direct use of the 'At' base is not allowed");
            }

            // ensure that at least one method is targeted
            if (targets.Length == 0)
                throw new MixinProcessorException("No methods targeted");
        }

        private void ScanMethod(MethodBase method, List<InjectorInfo> injectorsOut,
            List<MethodBase> methodsToInject, List<MethodBase> methodsToShadow, int mixinPriority)
        {
            Context.Logger.Debug($"scanning method {method}", IMixinLogger.LogType.SCAN);

            var injectors = method.GetCustomAttributes<Injector>(false).ToArray();
            var shadow = method.GetCustomAttributes<Shadow>(false).ToArray();

            if (shadow.Length > 0 && injectors.Length > 0)
                throw new MixinProcessorException("Method cannot be both an injector and shadow");

            if (shadow.Length > 0)
            {
                methodsToShadow.Add(method);
                return;
            }

            methodsToInject.Add(method);

            if (injectors.Length == 0)
                return;

            if (injectors[0].Disable)
                return;

            // get a list of injectors, this *should* have size 1
            // get a list of targets, this should have size >= 1
            var targets = method.GetCustomAttributes<Target>(false).ToArray();
            // get instructions that we want to select
            // this is optional, but is commonly used so we will handle it
            var selectors = method.GetCustomAttributes<At>(true).ToArray();

            ValidateInjector(injectors, targets, selectors);
            var injector = injectors[0];

            injectorsOut.Add(new InjectorInfo()
            {
                Injector = injector,
                Body = method,
                Targets = targets,
                Selectors = selectors,
                Priority = injector.Priority == int.MinValue ? mixinPriority : injector.Priority
            });
        }

        private MixinInfo ScanMixin(Type mixinType, Type[] targets, int mixinPriority)
        {
            // get all methods, practically
            var methods = Utils.ScanMethods(mixinType);

            MixinInfo mixinInfo = new()
            {
                MixinType = mixinType,
                TargetClasses = targets,
                Injectors = new(),
                MethodsToRemap = new(),
                ShadowMethods = new(),
                ShadowFields = new(),
            };

            foreach (var method in methods)
            {
                ScanMethod(method, mixinInfo.Injectors, mixinInfo.MethodsToRemap, mixinInfo.ShadowMethods, mixinPriority);
            }

            return mixinInfo;
        }

        private MixinInfo ProcessMixin(Type type)
        {
            Context.Logger.Debug("scanning mixin: " + type, IMixinLogger.LogType.SCAN);
            var attr = type.GetCustomAttribute<Mixin>();

            return ScanMixin(type, attr.Target.Select(u =>
            {
                var res = u.TryResolve(Context);
                if (res == null)
                    Context.Logger.Error($"failed to resolve type, ignoring {u.Name}");
                return res;
            }).Where(u => u != null).ToArray()!, attr.Priority);
        }

        public List<MixinInfo> ScanForMixins()
        {
            List<MixinInfo> info = new();
            foreach ((var annotatedType, _) in Context.GetTypesWithAttribute(typeof(Attributes.Mixin)))
            {
                info.Add(ProcessMixin(annotatedType));
            }
            return info;
        }
    }
}