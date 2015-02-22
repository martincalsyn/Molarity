using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Molarity.Hosting
{
    class Datagram
    {
        public Datagram(IPEndPoint endPoint, IPAddress localAddress, string message, bool persistent)
        {
            this.EndPoint = endPoint;
            this.LocalAddress = localAddress;
            this.Message = message;
            this.Persistent = persistent;
        }

        public async Task Send()
        {
            var msg = Encoding.ASCII.GetBytes(this.Message);
            try
            {
                var client = new UdpClient();
                client.Client.Bind(new IPEndPoint(this.LocalAddress, 0));
                try
                {
                    var result = await client.SendAsync(msg, msg.Length, this.EndPoint);
                    //Console.WriteLine("Sent: {0}", this.Message);
                }
                catch (Exception)
                {
                    //TODO: Logging and recovery
                    throw;
                }
                finally
                {
                    try
                    {
                        client.Close();
                    }
                    catch (Exception)
                    {
                        //TODO: Logging and recovery
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                //TODO: Logging and recovery
                //Error(ex);
            }
            ++SendCount;
        }
        public IPEndPoint EndPoint { get; private set; }

        public IPAddress LocalAddress { get; private set; }

        public string Message { get; private set; }

        public bool Persistent { get; private set; }

        public uint SendCount { get; private set; }

    }
}
