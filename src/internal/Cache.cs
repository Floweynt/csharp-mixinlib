namespace MixinLib.Internal
{
    public class Cache<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> Lookup = new();

        public TValue? Get(TKey key)
        {
            if (!Lookup.ContainsKey(key))
            {
                return default;
            }

            return Lookup[key];
        }
        public TValue Get(TKey key, Func<TKey, TValue> producer, bool forceUpdate = false)
        {
            if (!Lookup.ContainsKey(key) || forceUpdate)
            {
                Lookup[key] = producer(key);
            }

            return Lookup[key];
        }
    }
}