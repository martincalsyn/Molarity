using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Molarity.Hosting
{
    internal class SsdpHandler : IDisposable
    {
        private const int DatagramsPerMessage = 2;

        private const string SsdpAddressString = "239.255.255.250";

        private const int SSDP_PORT = 1900;

        private static readonly IPEndPoint SsdpEndpoint = new IPEndPoint(IPAddress.Parse(SsdpAddressString), SSDP_PORT);
        private static readonly IPAddress SsdpAddress = IPAddress.Parse(SsdpAddressString);
        private static readonly Random _random = new Random();

        private readonly UdpClient _client = new UdpClient();
        private readonly Timer _notificationTimer = new Timer(60000);
        private readonly Timer _sendTimer = new Timer(1000);
        private readonly Dictionary<Guid, SsdpService> _services = new Dictionary<Guid, SsdpService>();
        private readonly ConcurrentQueue<Datagram> _messageQueue = new ConcurrentQueue<Datagram>();
        private readonly CancellationTokenSource _ctsource = new CancellationTokenSource();
        private Task _processQueueTask;
        public SsdpHandler()
        {
            _notificationTimer.Elapsed += NotificationTimerOnElapsed;
            _notificationTimer.Enabled = true;
            _sendTimer.Elapsed += SendTimerOnElapsed;
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.ExclusiveAddressUse = false;
            _client.Client.Bind(new IPEndPoint(IPAddress.Any, SSDP_PORT));
            _client.JoinMulticastGroup(SsdpAddress, 2);

            Receive();
        }

        public void Dispose()
        {
            _notificationTimer.Enabled = false;
            // Request cancellation
            _ctsource.Cancel();
            // Wait for the queue to drain
            if (_processQueueTask!=null)
                _processQueueTask.Wait();
            _sendTimer.Enabled = false;
            // Sign off
            _client.DropMulticastGroup(SsdpAddress);
            _notificationTimer.Dispose();
            _sendTimer.Dispose();
        }

        public void RegisterService(Guid id, Uri location, IPAddress addr, params string[] serviceTypes)
        {
            SsdpService service;
            if (!_services.TryGetValue(id, out service))
            {
                service = new SsdpService(id, location, addr, serviceTypes);
                _services.Add(id, service);
            }
            NotifyAll();
        }
        private void SendTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (_ctsource.IsCancellationRequested)
                return;

            _processQueueTask = ProcessQueue(_ctsource.Token);
        }

        private void NotificationTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
        }

        internal void NotifyAll()
        {
            foreach (var d in _services.Values)
            {
                NotifyService(d, "alive", false);
            }
        }

        internal void NotifyService(SsdpService service, string notificationSubtype, bool persistent)
        {
            foreach (var type in service.ServiceTypes)
            {
                var headers = new Headers();
                headers.Add("HOST", "239.255.255.250:1900");
                headers.Add("CACHE-CONTROL", "max-age = 600");
                headers.Add("LOCATION", service.Location.AbsoluteUri);
                headers.Add("SERVER", SsdpHandler.Signature);
                headers.Add("NTS", "ssdp:" + notificationSubtype);
                headers.Add("NT", type);
                headers.Add("USN", service.Usn);

                SendDatagram(
                    SsdpEndpoint,
                    service.Address,
                    String.Format("NOTIFY * HTTP/1.1\r\n{0}\r\n", headers.ToString()),
                    persistent
                    );
            }
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

        private async Task ProcessQueue(CancellationToken ct)
        {
            Console.WriteLine("start");
            while (_messageQueue.Count != 0)
            {
                Datagram msg;
                if (!_messageQueue.TryPeek(out msg))
                {
                    continue;
                }
                if (msg != null && (!ct.IsCancellationRequested || msg.Persistent))
                {
                    msg.Send();
                    if (msg.SendCount > DatagramsPerMessage)
                    {
                        _messageQueue.TryDequeue(out msg);
                    }
                    break;
                }
                else
                {
                    _messageQueue.TryDequeue(out msg);
                }
            }
            _sendTimer.Enabled = _messageQueue.Count != 0;
            _sendTimer.Interval = _random.Next(50, !ct.IsCancellationRequested ? 300 : 100);
            Console.WriteLine("stop");
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
                if (string.IsNullOrEmpty(searchTerm))
                {
                    // Give them everything
                    foreach (var serviceType in service.ServiceTypes)
                    {
                        SendSearchResponse(ep, service, serviceType);
                    }
                }
                else if (service.ServiceTypes.Contains(searchTerm))
                {
                    SendSearchResponse(ep, service, searchTerm);
                }
            }
        }

        private void SendSearchResponse(IPEndPoint endpoint, SsdpService service, string serviceType)
        {
            var headers = new Headers();
            headers.Add("CACHE-CONTROL", "max-age = 600");
            headers.Add("DATE", DateTime.Now.ToString("R"));
            headers.Add("EXT", string.Empty);
            headers.Add("LOCATION", service.Location.ToString());
            headers.Add("ST", serviceType);
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
                              string message, bool persistent)
        {
            if (_ctsource.IsCancellationRequested)
                return;

            var dgram = new Datagram(endpoint, address, message, persistent);
            if (_messageQueue.Count == 0)
            {
                dgram.Send();
            }
            _messageQueue.Enqueue(dgram);
            _sendTimer.Enabled = true;
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
            "{0}{1}/{2}.{3} UPnP/2.0 OAS/1.0 molarity{4}.{5}",  //OAS is Oasis Automation Services
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
