using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Molarity.Hosting
{
    class SsdpService
    {
        private readonly HashSet<string> _serviceTypes = new HashSet<string>();

        public SsdpService(Guid uuid, Uri location, IPAddress address, IEnumerable<string> serviceTypes)
        {
            Uuid = uuid;
            foreach (var s in serviceTypes)
            {
                var typeId = s;
                if (typeId.StartsWith("uuid:", StringComparison.Ordinal))
                {
                    Usn = typeId;
                }
                else
                {
                    Usn = String.Format("uuid:{0}::{1}", Uuid, typeId);
                }

                _serviceTypes.Add(s);
            }
            Location = location;
            Address = address;

        }

        public IPAddress Address { get; set; }
        public HashSet<string> ServiceTypes { get { return _serviceTypes;  } }
        public Uri Location { get; set; }
        public string Usn { get; set; }
        public Guid Uuid { get; set; }
    }
}
