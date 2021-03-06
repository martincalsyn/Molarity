﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Remoting.Contexts;
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
        private HttpHandler _http;
        private int _httpPort = 7000;
        private bool _running = false;
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
            Stop();
        }
        public void Run()
        {
            if (_running)
                return;
            _running = true;

            _ssdp = new SsdpHandler();
            Guid id = Guid.NewGuid();
            _ssdp.RegisterService(id, "http://{0}:7000/Directory/upnp", "urn:molarity:directory");

            _http = new HttpHandler(_httpPort);

            _ssdp.Run();
            _http.Run();

            _http.AddService("/Directory/upnp", new UpnpDeviceDefinitionService());
        }

        public void Stop()
        {
            if (!_running)
                return;
            _running = false;

            _ssdp.Stop();
            _http.Stop();
        }
    }
}
