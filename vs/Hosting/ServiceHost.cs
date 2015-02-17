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

// Do this for newtonsoft:
// sudo apt-get install libnewtonsoft-json-cil-dev monodoc-newtonsoft-json-manual
using Newtonsoft.Json;

namespace OasisAutomation.Hosting
{
    public class ServiceHost : IDisposable
    {
        private HttpListener _httpListener;
        private UdpClient _udpClient;
        private IPAddress _groupAddress;
        private IPEndPoint _groupEndpoint;
        private int _udpPort = 7001;
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
            if (_udpClient != null)
            {
                _udpClient.DropMulticastGroup(_groupAddress);
                _udpClient = null;
            }
            if (_httpListener != null)
            {
                _httpListener.Stop();
                _httpListener = null;
            }
        }

        public void Start()
        {
            // Create the udp rendezvous channel
#if __MonoCS__
            _udpClient = new UdpClient(_udpPort, AddressFamily.InterNetwork);
            _groupAddress = IPAddress.Parse("239.0.0.222");
#else
            _udpClient = new UdpClient(_udpPort, AddressFamily.InterNetworkV6);
            _groupAddress = IPAddress.Parse("FF01::1");
#endif
            _udpClient.JoinMulticastGroup(_groupAddress);
            _groupEndpoint = new IPEndPoint(_groupAddress, _udpPort);

            // Create the http channel
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(string.Format("http://*:{0}/", _httpPort));

            Task.Run(() => Receiver());
            Task.Run(() => Sender());
            _httpListener.Start();
        }

        private async void Sender()
        {
            string localHostName = Dns.GetHostName();
            var localAddresses = await Dns.GetHostAddressesAsync(localHostName);

            var candidates = new List<string>();
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
            {
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork || addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        if (!addr.IsDnsEligible 
                            || addr.Address.IsIPv6LinkLocal 
                            || addr.Address.IsIPv6Multicast
#if !__MonoCS__
                            || addr.Address.IsIPv4MappedToIPv6
#endif
                            )
                            continue;
                        candidates.Add(string.Format("http://{0}:{1}/", addr.Address, _httpPort));
                    }
                }
            }

            var announcement = new
            {
                domain = "calsynshire",
                directoryUris = candidates.ToArray()
            };
            var announcementString = JsonConvert.SerializeObject(announcement);
            var buffer = Encoding.Unicode.GetBytes(announcementString);
            while (true)
            {
                await _udpClient.SendAsync(buffer, buffer.Length, _groupEndpoint);
                await Task.Delay(1000);
            }
        }

        private async void Receiver()
        {
            while (true)
            {
                var data = await _udpClient.ReceiveAsync();
                Console.WriteLine("Data received from " + data.RemoteEndPoint);
            }
        }
    }
}
