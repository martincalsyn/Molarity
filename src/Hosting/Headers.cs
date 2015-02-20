using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OasisAutomation.Hosting
{
    class Headers : IDictionary<string,string>
    {
        private readonly Dictionary<string, string> _dict = new Dictionary<string, string>();
        private readonly static Regex validator = new Regex(@"^[a-z\d][a-z\d_.-]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Stream HeaderStream
        {
            get
            {
                return new MemoryStream(Encoding.ASCII.GetBytes(this.ToString()));
            }
        }
        public override string ToString()
        {
            var hb = new StringBuilder();
            foreach (var h in _dict)
            {
                hb.AppendFormat("{0}: {1}\r\n", h.Key, h.Value);
            }
            return hb.ToString();
        }

        public void Add(string key, string value)
        {
            _dict.Add(key.ToUpperInvariant(), value);
        }

        public bool ContainsKey(string key)
        {
            return _dict.ContainsKey(key.ToUpperInvariant());
        }

        public ICollection<string> Keys
        {
            get { return _dict.Keys; }
        }

        public bool Remove(string key)
        {
            return _dict.Remove(key.ToUpperInvariant());
        }

        public bool TryGetValue(string key, out string value)
        {
            return _dict.TryGetValue(key.ToUpperInvariant(), out value);
        }

        public ICollection<string> Values
        {
            get { return _dict.Values; }
        }

        public string this[string key]
        {
            get { return _dict[key.ToUpperInvariant()]; }
            set { _dict[key.ToUpperInvariant()] = value; }
        }

        public void Add(KeyValuePair<string, string> item)
        {
            _dict.Add(item.Key.ToUpperInvariant(), item.Value);
        }

        public void Clear()
        {
            _dict.Clear();
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return _dict.Contains(new KeyValuePair<string, string>(item.Key.ToUpperInvariant(), item.Value));
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return _dict.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return _dict.Remove(item.Key.ToUpperInvariant());
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _dict.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _dict.GetEnumerator();
        }
    }
}
