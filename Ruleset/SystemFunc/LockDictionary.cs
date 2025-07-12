using System.Collections;
using System.Collections.Generic;

namespace oomtm450PuckMod_Ruleset.SystemFunc {
    /// <summary>
    /// Class containing a dictionary with an integrated lock for easier async unsafe thread code management.
    /// </summary>
    public class LockDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> {
        #region Fields
        private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();

        private readonly object _locker = new object();
        #endregion

        #region Properties
        public Dictionary<TKey, TValue>.KeyCollection Keys {get => _dictionary.Keys; }

        public Dictionary<TKey, TValue>.ValueCollection Values { get => _dictionary.Values; }

        public int Count { get => _dictionary.Count; }
        #endregion

        #region Constructors
        public LockDictionary() { }

        public LockDictionary(Dictionary<TKey, TValue> dictionary) {
            _dictionary = dictionary;
        }
        #endregion

        #region Methods/Functions
        public TValue this[TKey index] {
            get {
                lock (_locker)
                    return _dictionary[index];
            }
            set {
                lock (_locker)
                    _dictionary[index] = value;
            }
        }

        public bool TryGetValue(TKey key, out TValue value) {
            lock (_locker)
                return _dictionary.TryGetValue(key, out value);
        }

        public void Clear() {
            lock (_locker)
                _dictionary.Clear();
        }

        public void Add(TKey key, TValue value) {
            lock (_locker)
                _dictionary.Add(key, value);
        }

        public bool Remove(TKey key) {
            lock (_locker)
                return _dictionary.Remove(key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            lock (_locker)
                return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        #endregion
    }
}
