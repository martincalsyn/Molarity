using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

// On windows, be sure to open up access to the port
//  netsh http add urlacl url=http://+:80/MyUri user=DOMAIN\user

// Do this one time for newtonsoft on Mono via nuget
//sudo mozroots --import --machine --sync
//$ sudo certmgr -ssl -m https://go.microsoft.com
//$ sudo certmgr -ssl -m https://nugetgallery.blob.core.windows.net
//$ sudo certmgr -ssl -m https://nuget.org// 
// At first, I did this on Rpi, but nuget is better
//sudo apt-get install libnewtonsoft-json-cil-dev monodoc-newtonsoft-json-manual
using Newtonsoft.Json;

namespace Molarity.Hosting
{
    public class ServiceHost : IDisposable
    {
        private SsdpHandler _ssdp;
        private HttpListener _httpListener;
        private int _httpPort = 7000;
        public static async Task<ServiceHost> Create()
        {
            var result = new ServiceHost();
            //await result.InitAsync();
            return result;
        }

        private ServiceHost()
        {
        }

        ~ServiceHost()
        {
            Dispose();
        }

        //private async Task InitAsync()
        //{
        //}

        public void Dispose()
        {
            Close();
        }
        public void Close()
        {
            if (_httpListener != null)
            {
                _httpListener.Stop();
                _httpListener = null;
            }
        }

        public void Start()
        {
            _ssdp = new SsdpHandler();
            Guid id = Guid.NewGuid();
            _ssdp.RegisterService(id, "http://{0}:7000/Directory/upnp",
                "urn:molarity:directory");

            // Create the http channel
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(string.Format("http://*:{0}/", _httpPort));
            _httpListener.Start();
        }
    }
}
