namespace MixinLib.Internal
{
    public class DefaultedDict<TKey, TValue> : Dictionary<TKey, TValue>
        where TValue : new()
    {
        public new TValue this[TKey key]
        {
            get {
                if (!ContainsKey(key))
                    base[key] = new TValue();
                return base[key];
            }
            set { base[key] = value; }
        }
    }
}