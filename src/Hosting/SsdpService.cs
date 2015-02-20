using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OasisAutomation.Hosting
{
    class SsdpService
    {
        public IPAddress Address { get; set; }
        public string ServiceType { get; set; }
        public Uri Location { get; set; }
        public string Usn { get; set; }
        public Guid Uuid { get; set; }
    }
}
