using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Molarity.Hosting
{
    class HttpHandler
    {
        private HttpListener _http;
        private readonly int _httpPort;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _running = false;

        public HttpHandler(int port)
        {
            _httpPort = port;
        }

        public void Run()
        {
            if (_running)
                return;
            _running = true;
            Task.Run(() => RunTask(), _cts.Token);
        }

        private async Task RunTask()
        {
            try
            {
                // Create the http channel
                _http = new HttpListener();
                _http.Prefixes.Add(string.Format("http://*:{0}/", _httpPort));
                _http.Start();

                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var context = await _http.GetContextAsync();
                        try
                        {
                            ProcessRequest(context);
                        }
                        catch (Exception exc)
                        {
                            // request processing exceptions should never break the listen loop
                            Console.WriteLine(exc);
                        }
                    }
                    catch (HttpListenerException)
                    {
                        // overlook this and try to carry on
                        if (_cts.IsCancellationRequested)
                            break;
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc);
                        //TODO: it could be bad to suppress all exceptions here because it could result in a tight loop. Set some sort of failure counter
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // not an error - return silently
            }            
        }

        public void Stop()
        {
            if (!_running)
                return;
            _running = false;

            _cts.Cancel();
            if (_http != null)
            {
                _http.Stop();
                _http = null;
            }
        }

        private void ProcessRequest(HttpListenerContext ctx)
        {
            string msg = string.Format("HTTP request from {2} on {3} : {0} {1}", ctx.Request.HttpMethod, ctx.Request.Url, ctx.Request.RemoteEndPoint, ctx.Request.LocalEndPoint);
            Console.WriteLine(msg);

            var sb = new StringBuilder();
            sb.Append("<html><body><h1>" + "Hi there" + "</h1>");
            sb.Append("</body></html>");

            var data = Encoding.UTF8.GetBytes(sb.ToString());
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
            ctx.Response.OutputStream.Close();
        }

    }
}
