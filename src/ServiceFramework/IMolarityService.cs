using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Molarity.Services
{
    public interface IMolarityService
    {
        Task GetHandler(HttpListenerContext ctx);
    }
}
