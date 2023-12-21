using MixinLib.Attributes;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace MixinLib.Internal
{
    public static partial class Utils
    {
        public static int RemoveAll<T>(this IList<T> list, Predicate<T> match)
        {
            int count = 0;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (match(list[i]))
                {
                    ++count;
                    list.RemoveAt(i);
                }
            }

            return count;
        }

        public static void Deconstruct<K, V>(this KeyValuePair<K, V> self, out K k, out V v)
        {
            k = self.Key;
            v = self.Value;
        }

        // helper methods
        public static int AllocateLocal(this ILContext context, Type type)
        {
            var vars = context.Body.Variables;
            var id = vars.Count;
            vars.Add(new VariableDefinition(context.Import(type)));
            return id;
        }

        public static void Each<T>(this IEnumerable<T> ie, Action<T, int> action)
        {
            var i = 0;
            foreach (var e in ie) action(e, i++);
        }

        public static T[] Fill<T>(this T[] arr, T value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }
            return arr;
        }

        public static string GetInjDiagId(InjectorInfo info)
            => $"injector '{info.Injector.Id ?? info.Injector.GetType().Name}' in {info.Body.DeclaringType.FullName}";
        public static string GetSelDiagId(At selector, InjectorInfo info)
            => $"selector '{selector.Id ?? selector.GetType().Name}' in {info.Body.DeclaringType.FullName}";

        public static IEnumerable<T> EnumerableOf<T>(params T[] args)
        {
            return args;
        }
    }
}