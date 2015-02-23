using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Molarity.Services;

namespace Molarity.Hosting
{
    class MolarityService
    {
        private readonly IMolarityService _service;
        public MolarityService(IMolarityService impl)
        {
            _service = impl;
        }
        public Task ProcessRequest(HttpListenerContext ctx)
        {
            var method = ctx.Request.HttpMethod.ToLowerInvariant();
            switch (method)
            {
                case "get":
                    return GetHandler(ctx);
                case "put":
                    return PutHandler(ctx);
                case "delete":
                    return DeleteHandler(ctx);
                case "patch":
                    return PatchHandler(ctx);
                default:
                    throw new NotImplementedException();
            }
        }
        private Task GetHandler(HttpListenerContext ctx)
        {
            return _service.GetHandler(ctx);
        }

        private Task PutHandler(HttpListenerContext ctx)
        {
            throw new NotImplementedException();
        }

        private Task DeleteHandler(HttpListenerContext ctx)
        {
            throw new NotImplementedException();
        }

        private Task PatchHandler(HttpListenerContext ctx)
        {
            throw new NotImplementedException();
        }

    }
}
