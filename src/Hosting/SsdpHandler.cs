using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Molarity.Hosting
{
    class AnnouncementInterface
    {
        public IPAddress Address { get; set; }
        public UdpClient Client { get; set; }

        public async Task<Tuple<AnnouncementInterface, UdpReceiveResult>> ReceiveAsync()
        {
            var udpResult = await this.Client.ReceiveAsync();
            return new Tuple<AnnouncementInterface, UdpReceiveResult>(this, udpResult);
        }
    }

    internal class SsdpHandler : IDisposable
    {
        private const int DatagramsPerMessage = 2;

        private const string SsdpAddressString = "239.255.255.250";
        private const string MolarityDirectorySignature = "urn:molarity:directory";

        private const int SSDP_PORT = 1900;

        private static readonly IPEndPoint SsdpEndpoint = new IPEndPoint(IPAddress.Parse(SsdpAddressString), SSDP_PORT);
        private static readonly IPAddress SsdpAddress = IPAddress.Parse(SsdpAddressString);
        private static readonly Random _random = new Random();
        private static readonly TimeSpan DefaultCacheExpiry = TimeSpan.FromSeconds(600);

        private readonly Timer _notificationTimer = new Timer(60000);
        private readonly Dictionary<Guid, SsdpService> _services = new Dictionary<Guid, SsdpService>();
        private readonly ConcurrentQueue<Datagram> _messageQueue = new ConcurrentQueue<Datagram>();
        private readonly CancellationTokenSource _ctsource = new CancellationTokenSource();
        private readonly Task _processQueueTask;
        private readonly List<AnnouncementInterface> _interfaces = new List<AnnouncementInterface>();
        private readonly Dictionary<Guid, PeerNode> _peers = new Dictionary<Guid, PeerNode>();

        public SsdpHandler()
        {
            PopulateInterfaceList();

            _notificationTimer.Elapsed += NotificationTimerOnElapsed;
            _notificationTimer.Enabled = true;
            _processQueueTask = ProcessQueue(_ctsource.Token);
            Receive();
        }

        public void Dispose()
        {
            _notificationTimer.Enabled = false;
            // Request cancellation
            _ctsource.Cancel();
            // Wait for the queue to drain
            if (_processQueueTask != null)
                _processQueueTask.Wait();
            // Sign off
            foreach (var annIf in _interfaces)
            {
                if (annIf.Client != null)
                {
                    annIf.Client.DropMulticastGroup(SsdpAddress);
                }
            }
            _notificationTimer.Dispose();
        }

        private void PopulateInterfaceList()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            _interfaces.Clear();
            foreach (var nic in nics)
            {
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork ||
                        addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
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
                        annIf.Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                            true);
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
#if __MonoCS__
							var localEndpoint = new IPEndPoint(IPAddress.Any, SSDP_PORT);
#else
							var localEndpoint = new IPEndPoint(addr.Address, SSDP_PORT);
#endif
                            annIf.Client.Client.Bind(localEndpoint);
                        }
                        else
                        {
                            continue;
                        }
                        annIf.Client.MulticastLoopback = false;
                        annIf.Client.JoinMulticastGroup(SsdpAddress, addr.Address);
                        Console.WriteLine("Adding UPnP announcement interface on " + annIf.Address);
                        _interfaces.Add(annIf);
#if __MonoCS__
						// Mono does not work with bound endpoints, so we use IPAddress.Any and just the first valid interface
						break;
#endif
                    }
                }
            }
        }
        public void RegisterService(Guid id, string location, params string[] serviceTypes)
        {
            SsdpService service;
            if (!_services.TryGetValue(id, out service))
            {
                service = new SsdpService(id, location, serviceTypes);
                _services.Add(id, service);
            }
            NotifyAll();
        }

        internal void UnregisterService(Guid id)
        {
            SsdpService service;
            if (!_services.TryGetValue(id, out service))
            {
                return;
            }
            _services.Remove(id);
            foreach (var intf in _interfaces)
            {
                foreach (var d in _services.Values)
                {
                    NotifyService(intf, d, "byebye", true);
                }
            }
        }
        private void NotificationTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            Console.WriteLine("Sending UPnP notifications of our services");
            _notificationTimer.Interval = _random.Next(60000, 120000);
            NotifyAll();
            CleanUp();
        }

        private void CleanUp()
        {
            lock (_peers)
            {
                var now = DateTime.UtcNow;
                foreach (var peer in _peers.Values.ToArray())
                {
                    if (peer.Expiry < now)
                    {
                        Console.WriteLine("Removing expired peer {0} at {1}", peer, peer.Location);
                        _peers.Remove(peer.Id);
                    }
                }
            }
        }

        private void NotifyAll()
        {
            foreach (var intf in _interfaces)
            {
                foreach (var d in _services.Values)
                {
                    NotifyService(intf, d, "alive", false);
                }
            }
        }

        private void NotifyService(AnnouncementInterface intf, SsdpService service, string notificationSubtype, bool persistent)
        {
            foreach (var type in service.ServiceTypes)
            {
                var headers = new Headers();
                headers.Add("HOST", "239.255.255.250:1900");
                headers.Add("CACHE-CONTROL", "max-age = 600");
                headers.Add("LOCATION", service.GetLocation(intf));
                headers.Add("SERVER", SsdpHandler.Signature);
                headers.Add("NTS", "ssdp:" + notificationSubtype);
                headers.Add("NT", type);
                headers.Add("USN", service.Usn);

                SendDatagram(
                    SsdpEndpoint,
                    intf.Address,
                    String.Format("NOTIFY * HTTP/1.1\r\n{0}\r\n", headers.ToString()),
                    persistent
                    );
            }
        }

        private async void Receive()
        {
            var tasks = _interfaces.Select(annIf => annIf.ReceiveAsync()).ToList();
            while (true)
            {
                var fired = await Task.WhenAny(tasks);
                tasks.Remove(fired);
                var result = fired.Result;
                ProcessReceivedMessage(result.Item1, result.Item2);
                var task = result.Item1.ReceiveAsync();
                tasks.Add(task);
            }
        }

        private async void ProcessReceivedMessage(AnnouncementInterface intf, UdpReceiveResult udpResult)
        {
            var remoteEp = udpResult.RemoteEndPoint;
            var receivedData = udpResult.Buffer;
            if (receivedData == null || receivedData.Length == 0)
                return;

            using (var reader = new StreamReader(
                new MemoryStream(receivedData), Encoding.ASCII))
            {
                var line = reader.ReadLine();
                if (!string.IsNullOrEmpty(line))
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        return;
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
                    if (method == "M-SEARCH")
                    {
                        RespondToSearch(intf, remoteEp, headers["st"]);
                    }
                    if (method == "NOTIFY")
                    {
                        HandleNotify(intf, remoteEp, headers);
                    }
                }
            }
        }

        private async Task ProcessQueue(CancellationToken ct)
        {
            while (true)
            {
                try
                {
                    while (_messageQueue.Count != 0)
                    {
                        Datagram msg;
                        if (!_messageQueue.TryPeek(out msg))
                        {
                            continue;
                        }
                        // We must drain queue of persistent messages before exiting
                        // Persistent messages have to complete their send count before we can exit
                        if (msg != null && (!ct.IsCancellationRequested || msg.Persistent))
                        {
                            await msg.Send();
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
                    if (ct.IsCancellationRequested)
                        break;
                    // randomize the resend interval
                    var delay = _random.Next(50, 300);
                    await Task.Delay(delay, ct);
                    if (ct.IsCancellationRequested)
                        break;
                }
                catch (Exception)
                {
                    //TODO: report error, but don't rethrow - this loop needs to continue running
                }
            }
            //TODO: log termination
        }

        private void RespondToSearch(AnnouncementInterface intf, IPEndPoint ep, string searchTerm)
        {
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
                        SendSearchResponse(intf, ep, service, serviceType);
                    }
                }
                else if (service.ServiceTypes.Contains(searchTerm))
                {
                    SendSearchResponse(intf, ep, service, searchTerm);
                }
            }
        }


        private void HandleNotify(AnnouncementInterface intf, IPEndPoint remoteEp, Headers headers)
        {
            // Not one of ours?
            if (headers["NT"] != MolarityDirectorySignature)
                return;
            var usnTokens = headers["USN"].Split(':');
            if (usnTokens.Length < 2)
                return;
            if (usnTokens[0] != "uuid")
                return;
            Guid id;
            if (!Guid.TryParse(usnTokens[1], out id))
                return;

            PeerNode peer;
            if (headers["nts"] == "ssdp:alive")
            {
                lock (_peers)
                {
                    Uri location;
                    if (!headers.ContainsKey("LOCATION"))
                        return;
                    if (!Uri.TryCreate(headers["LOCATION"], UriKind.Absolute, out location))
                        return;

                    if (!_peers.TryGetValue(id, out peer))
                    {
                        peer = new PeerNode(id, location);
                        _peers.Add(id, peer);
                        Console.WriteLine("Discovered new peer {0} at {1}", peer.Id, peer.Location);
                    }
                    else
                    {
                        // update last-seen timestamp
                        peer.LastSeen = DateTime.UtcNow;
                        peer.Location = location;
                    }
                }

                bool expiryWasSet = false;
                if (headers.ContainsKey("CACHE-CONTROL"))
                {
                    //TODO: set expiration time
                    var ccTokens = headers["CACHE-CONTROL"].Split(',');
                    foreach (var token in ccTokens)
                    {
                        var kvTokens = token.Split('=');
                        if (kvTokens.Length > 1)
                        {
                            var left = kvTokens[0].Trim();
                            var right = kvTokens[1].Trim();
                            if (left.ToLowerInvariant() == "max-age")
                            {
                                int ageInSeconds;
                                if (Int32.TryParse(right, out ageInSeconds))
                                {
                                    peer.Expiry = peer.LastSeen + TimeSpan.FromSeconds(ageInSeconds);
                                    expiryWasSet = true;
                                }
                            }
                        }
                    }
                }
                if (!expiryWasSet)
                {
                    peer.Expiry = peer.LastSeen + DefaultCacheExpiry;
                }
            }
            else if (headers["NTS"] == "ssdp:byebye")
            {
                lock (_peers)
                {
                    if (_peers.TryGetValue(id, out peer))
                    {
                        Console.WriteLine("Service {0} at {1} has signed off", id, _peers[id].Location);
                        _peers.Remove(id);
                    }
                }
            }
        }

        private void SendSearchResponse(AnnouncementInterface intf, IPEndPoint endpoint, SsdpService service, string serviceType)
        {
            var headers = new Headers();
            headers.Add("CACHE-CONTROL", "max-age = 600");
            headers.Add("DATE", DateTime.Now.ToString("R"));
            headers.Add("EXT", string.Empty);
            headers.Add("LOCATION", service.GetLocation(intf));
            headers.Add("ST", serviceType);
            headers.Add("USN", service.Usn);

            SendDatagram(
              endpoint,
              intf.Address,
              String.Format("HTTP/1.1 200 OK\r\n{0}\r\n", headers.ToString()),
              false
              );
        }

        private void SendDatagram(IPEndPoint endpoint, IPAddress address,
                              string message, bool persistent)
        {
            if (_ctsource.IsCancellationRequested)
                return;

            var dgram = new Datagram(endpoint, address, message, persistent);
            _messageQueue.Enqueue(dgram);
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
            return String.Format("{0}{1}/{2}.{3} UPnP/2.0 OAS/1.0 molarity{4}.{5}", //OAS is Oasis Automation Services
                pstring,
                IntPtr.Size*8,
                os.Version.Major,
                os.Version.Minor,
                Assembly.GetExecutingAssembly().GetName().Version.Major,
                Assembly.GetExecutingAssembly().GetName().Version.Minor
                );
        }

    }
}
