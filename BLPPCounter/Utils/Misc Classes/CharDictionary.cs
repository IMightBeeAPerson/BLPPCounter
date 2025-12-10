using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal class CharDictionary<T> : IEnumerable<KeyValuePair<char, T>>
    {
        private readonly char[] keys;
        private readonly T[] values;

        public IEnumerable<char> Keys => keys;

        public IEnumerable<T> Values => values;

        public int Count => keys.Length;

        public CharDictionary(params char[] keys)
        {
            this.keys = keys;
            values = new T[(int)Math.Pow(2, 8 * Marshal.SizeOf(typeof(char)))];
        }
        public CharDictionary((char key, T value)[] pairs) : this(pairs.Select(p => p.key).ToArray())
        {
            foreach (var (key, value) in pairs)
                values[key] = value;
        }

        public bool ContainsKey(char key) => !values[key].Equals(default(T));
        public IEnumerator<KeyValuePair<char, T>> GetEnumerator()
        {
            foreach (var key in keys)
                yield return new KeyValuePair<char, T>(key, values[key]);
        }
        public bool TryGetValue(char key, out T value)
        {
            value = values[key];
            return !value.Equals(default(T));
        }
        public void SetValue(char key, T value) => values[key] = value;
        public T this[char key]
        {
            get => values[key];
            set => SetValue(key, value);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
