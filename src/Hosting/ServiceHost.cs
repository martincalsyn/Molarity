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

    class AnnouncementInterface
    {
        public IPAddress Address { get; set; }
        public UdpClient Client { get; set; }
        public IPAddress GroupAddress { get; set; }
        public IPEndPoint GroupEndpoint { get; set; }
    }
    public class ServiceHost : IDisposable
    {
        private SsdpHandler _ssdp;
        private HttpListener _httpListener;
        private int _udpPort = 7001;
        private int _httpPort = 7000;
        private List<AnnouncementInterface> _announcementIfs;
        public static async Task<ServiceHost> Create()
        {
            var result = new ServiceHost();
            //await result.InitAsync();
            return result;
        }

        private ServiceHost()
        {
            _ssdp = new SsdpHandler();
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
            foreach (var annIf in _announcementIfs)
            {
                if (annIf.Client != null)
                {
                    annIf.Client.DropMulticastGroup(annIf.GroupAddress);
                }
            }
            _announcementIfs = new List<AnnouncementInterface>();
            if (_httpListener != null)
            {
                _httpListener.Stop();
                _httpListener = null;
            }
        }

        public void Start()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            _announcementIfs = new List<AnnouncementInterface>();
            foreach (var nic in nics)
            {
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork || addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        if (!addr.IsDnsEligible
                            || IPAddress.IsLoopback(addr.Address)
                            || addr.Address.IsIPv6LinkLocal
#if !__MonoCS__
                            // This is not defined for Mono
                            || addr.Address.IsIPv4MappedToIPv6
#endif
                            )
                        {
                            continue;
                        }
                        var annIf = new AnnouncementInterface()
                        {
                            Address = addr.Address,
                            Client = new UdpClient()
                        };
                        annIf.Client.ExclusiveAddressUse = false;
                        annIf.Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var localEndpoint = new IPEndPoint(addr.Address, _udpPort);
                            annIf.Client.Client.Bind(localEndpoint);
                            annIf.GroupAddress = IPAddress.Parse("239.0.0.222");
                        }
                        else
                        {
                            continue;
                            //var localEndpoint = new IPEndPoint(addr.Address, _udpPort);
                            //annIf.Client.Client.Bind(localEndpoint);
                            //annIf.GroupAddress = IPAddress.Parse("FF01::1");
                        }
                        annIf.Client.MulticastLoopback = false;
                        annIf.Client.JoinMulticastGroup(annIf.GroupAddress, addr.Address);
                        annIf.GroupEndpoint = new IPEndPoint(annIf.GroupAddress, _udpPort);
                        Console.WriteLine("Adding announcement address " + annIf.Address);
                        _announcementIfs.Add(annIf);
                    }
                }
            }

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

            // Build a list of candidate http listening endpoints
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

            // Build the UDP announcement
            var announcement = new
            {
                domain = "calsynshire",
                directoryUris = candidates.ToArray()
            };
            var announcementString = JsonConvert.SerializeObject(announcement);
            var buffer = Encoding.Unicode.GetBytes(announcementString);

            // Send the announcement
            while (true)
            {
                foreach (var annIf in _announcementIfs)
                {
                    await annIf.Client.SendAsync(buffer, buffer.Length, annIf.GroupEndpoint);                    
                }
                await Task.Delay(1000);
            }
        }

        private async void Receiver()
        {
            while (true)
            {
                var tasks = new List<Task<UdpReceiveResult>>();
                foreach (var annIf in _announcementIfs)
                {
                    var task = annIf.Client.ReceiveAsync();
                    tasks.Add(task);
                }
                var fired = await Task.WhenAny(tasks);
                var data = fired.Result;
                Console.WriteLine("Data received from " + data.RemoteEndPoint);
            }
        }
    }
}
