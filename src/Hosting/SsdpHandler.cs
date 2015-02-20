using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace OasisAutomation.Hosting
{
    internal class SsdpHandler : IDisposable
    {
        private const int DATAGRAMS_PER_MESSAGE = 2;

        private const string SsdpAddressString = "239.255.255.250";

        private const int SSDP_PORT = 1900;

        private static readonly IPEndPoint SsdpEndpoint = new IPEndPoint(IPAddress.Parse(SsdpAddressString), SSDP_PORT);
        private static readonly IPAddress SsdpAddress = IPAddress.Parse(SsdpAddressString);

        private readonly UdpClient _client = new UdpClient();
        private readonly Timer _notificationTimer = new Timer(60000);
        private readonly Dictionary<Guid,SsdpService> _services = new Dictionary<Guid, SsdpService>();
        public SsdpHandler()
        {
            _notificationTimer.Elapsed += NotificationTimerOnElapsed;
            _notificationTimer.Enabled = true;

            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.ExclusiveAddressUse = false;
            _client.Client.Bind(new IPEndPoint(IPAddress.Any, SSDP_PORT));
            _client.JoinMulticastGroup(SsdpAddress, 2);

            Receive();
        }

        public void Dispose()
        {
            _client.DropMulticastGroup(SsdpAddress);
            _notificationTimer.Enabled = false;
            _notificationTimer.Dispose();
        }

        private void NotificationTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
        }

        private async void Receive()
        {
            while (true)
            {
                UdpReceiveResult? udpResult = null;
                try
                {
                    udpResult = await _client.ReceiveAsync();
                }
                catch (ObjectDisposedException)
                {
                    udpResult = null;
                }
                if (!udpResult.HasValue)
                    continue;

                var remoteEp = udpResult.Value.RemoteEndPoint;
                var receivedData = udpResult.Value.Buffer;
                if (receivedData == null || receivedData.Length == 0)
                    continue;

                using (var reader = new StreamReader(
                    new MemoryStream(receivedData), Encoding.ASCII))
                {
                    var line = reader.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }
                        var method = line.Split(new[] {' '}, 2)[0];
                        var headers = new Headers();
                        for (line = reader.ReadLine(); line != null; line = reader.ReadLine())
                        {
                            line = line.Trim();
                            if (string.IsNullOrEmpty(line))
                            {
                                break;
                            }
                            var parts = line.Split(new[] {':'}, 2);
                            headers[parts[0]] = parts[1].Trim();
                        }
                        Debug.WriteLine("{0} - Datagram method: {1}", remoteEp, method);
                        if (method == "M-SEARCH")
                        {
                            RespondToSearch(remoteEp, headers["st"]);
                        }
                    }
                }
            }
        }

        private void RespondToSearch(IPEndPoint ep, string searchTerm)
        {
            Console.WriteLine("SSDP Search for {0} from {1}", searchTerm, ep);
            if (searchTerm == "ssdp:all")
            {
                // make this into a wildcard matching all services
                searchTerm = null;
            }

            foreach (var service in _services.Values)
            {
                if (string.IsNullOrEmpty(searchTerm) || searchTerm == service.ServiceType)
                {
                    SendSearchResponse(ep, service);
                }
            }
        }

        private void SendSearchResponse(IPEndPoint endpoint, SsdpService service)
        {
            var headers = new Headers();
            headers.Add("CACHE-CONTROL", "max-age = 600");
            headers.Add("DATE", DateTime.Now.ToString("R"));
            headers.Add("EXT", string.Empty);
            headers.Add("LOCATION", service.Location.ToString());
            headers.Add("ST", service.ServiceType);
            headers.Add("USN", service.Usn);

            SendDatagram(
              endpoint,
              service.Address,
              String.Format("HTTP/1.1 200 OK\r\n{0}\r\n", headers.ToString()),
              false
              );
            //InfoFormat(
            //  "{2}, {1} - Responded to a {0} request", dev.Type, endpoint,
            //  dev.Address);
        }

        private void SendDatagram(IPEndPoint endpoint, IPAddress address,
                              string message, bool sticky)
        {
            //var dgram = new Datagram(endpoint, address, message, sticky);
            //if (messageQueue.Count == 0)
            //{
            //    dgram.Send();
            //}
            //messageQueue.Enqueue(dgram);
            //queueTimer.Enabled = true;
        }

        private static string _sig;
        public static string Signature
        {
            get
            {
                if (_sig == null)
                {
                    _sig = GenerateSignature();
                }
                return _sig;
            }
        }
        private static string GenerateSignature()
        {
            var os = Environment.OSVersion;
            var pstring = os.Platform.ToString();
            switch (os.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                    pstring = "WIN";
                    break;
                default:
                    break;
            }
            return String.Format(
            "{0}{1}/{2}.{3} UPnP/2.0 OAS/1.0 oasishost{4}.{5}",  //OAS is Oasis Automation Services
            pstring,
            IntPtr.Size * 8,
            os.Version.Major,
            os.Version.Minor,
            Assembly.GetExecutingAssembly().GetName().Version.Major,
            Assembly.GetExecutingAssembly().GetName().Version.Minor
            );
        }

    }
}
