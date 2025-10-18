using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Codebase {
    /// <summary>
    /// Class containing a list with an integrated lock for easier async unsafe thread code management.
    /// </summary>
    public class LockList<T> : IEnumerable<T> {
        #region Fields
        private readonly List<T> _list = new List<T>();

        private readonly object _locker = new object();
        #endregion

        #region Properties
        public int Count { get => _list.Count; }
        #endregion

        #region Constructors
        public LockList() { }

        public LockList(IEnumerable<T> collection) {
            lock (_locker)
                _list = new List<T>(collection);
        }
        #endregion

        #region Methods/Functions
        public T this[int index] {
            get {
                lock (_locker)
                    return _list[index];
            }
            set {
                lock (_locker)
                    _list[index] = value;
            }
        }

        public void Clear() {
            lock (_locker)
                _list.Clear();
        }

        public void Add(T value) {
            lock (_locker)
                _list.Add(value);
        }

        public bool Remove(T value) {
            lock (_locker)
                return _list.Remove(value);
        }

        public T First() {
            lock (_locker)
                return _list.First();
        }

        public bool Contains(T item) {
            lock (_locker)
                return _list.Contains(item);
        }

        public int IndexOf(T item) {
            lock (_locker)
                return _list.IndexOf(item);
        }

        public IEnumerator<T> GetEnumerator() {
            lock (_locker)
                return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        #endregion
    }
}
