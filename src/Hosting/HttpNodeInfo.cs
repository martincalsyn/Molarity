using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Molarity.Hosting
{
    class HttpNodeInfo
    {
        private readonly Dictionary<string, HttpNodeInfo> _children = new Dictionary<string, HttpNodeInfo>();
        private MolarityService _service;
        public HttpNodeInfo()
        {

        }

        public HttpNodeInfo(string name)
        {
            this.Name = name;
        }
        public string Name { get; private set; }
        public IDictionary<string, HttpNodeInfo> Children { get { return _children; } }

        public MolarityService Service
        {
            get { return _service; }
            set
            {
                if (value != null && _service != null)
                {
                    throw new Exception("Service already set");
                }
                _service = value;
            }
        }
    }
}
