using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Molarity.Hosting
{
    class PeerNode
    {
        public PeerNode(Guid id, Uri location)
        {
            this.Id = id;
            this.LastSeen = DateTime.UtcNow;
            this.Location = location;
        }

        public Guid Id { get; private set; }
        public Uri Location { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime Expiry { get; set; }
    }
}
