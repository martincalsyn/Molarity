using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Molarity.Services;

namespace Molarity.Hosting
{
    public class UpnpDeviceDefinitionService : IMolarityService
    {
        //TODO: this should persist across reboots. Achieve that by saving the XML as our state
        private Guid _nodeId = Guid.NewGuid();

        private readonly List<string> _template = new List<string>()
        {
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n",
            "<root xmlns=\"urn:schemas-upnp-org:device-1-0\">\n",
            "  <specVersion>\n",
            "    <major>1</major>\n",
            "    <minor>0</minor>\n",
            "  </specVersion>\n",
            "\n",
            "  <device>\n",
            "    <deviceType>urn:schemas-molarity-org:node</deviceType>\n",
            "    <friendlyName>Molarity Node</friendlyName>\n",
            "    <manufacturer>The Molarity Automation Project</manufacturer>\n",
            "    <modelName>Molarity Services Node</modelName>\n",
            "    <UDN>uuid:{nodeId}</UDN>\n",
            "\n",
            "    <serviceList>\n",
            "    </serviceList>\n",
            "  </device>\n",
            "</root>\n",
        };
        public UpnpDeviceDefinitionService()
        {
        }

        public Task GetHandler(HttpListenerContext ctx)
        {
            var sb = new StringBuilder();
            foreach (var line in _template)
            {
                var result = line.Replace("{nodeId}", Guid.NewGuid().ToString("D"));
                sb.Append(result);
            }

            var data = Encoding.UTF8.GetBytes(sb.ToString());
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.ContentType = "application/xml";
            ctx.Response.OutputStream.Write(data, 0, data.Length);
            ctx.Response.OutputStream.Close();

            return Task.FromResult(0);
        }
    }
}
