using System.Collections;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// A dictionary that iterates in insertion order, like Godot's Dictionary (the sim steps players in the order they joined).
    /// Removing keeps the order of the rest.
    /// </summary>
    public sealed class OrderedDict<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        readonly List<TKey> _keys = new List<TKey>();
        readonly Dictionary<TKey, TValue> _map = new Dictionary<TKey, TValue>();

        public int Count => _keys.Count;
        public IReadOnlyList<TKey> Keys => _keys;

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (TKey key in _keys) yield return _map[key];
            }
        }

        public TValue this[TKey key]
        {
            get => _map[key];
            set
            {
                if (!_map.ContainsKey(key)) _keys.Add(key);
                _map[key] = value;
            }
        }

        public bool ContainsKey(TKey key) => _map.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _map.TryGetValue(key, out value);

        /// <summary>The value for `key`, or the type's default (GDScript's `d[k] if d.has(k) else null`).</summary>
        public TValue GetOrDefault(TKey key, TValue fallback = default) => _map.TryGetValue(key, out TValue v) ? v : fallback;

        public bool Remove(TKey key)
        {
            if (!_map.Remove(key)) return false;
            _keys.Remove(key);
            return true;
        }

        public void Clear()
        {
            _keys.Clear();
            _map.Clear();
        }

        /// <summary>A snapshot of the keys (safe to change the dictionary while iterating it).</summary>
        public List<TKey> KeyList() => new List<TKey>(_keys);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < _keys.Count; i++)
                yield return new KeyValuePair<TKey, TValue>(_keys[i], _map[_keys[i]]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
