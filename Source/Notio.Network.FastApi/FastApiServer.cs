using Notio.Network.FastApi.Enums;
using Notio.Network.FastApi.Attributes;
using Notio.Shared.Configuration;
using System;
using System.Collections.Generic;
using System.Net;

namespace Notio.Network.FastApi
{
    public class FastApiServer
    {
        private readonly HttpListener _listener;
        private readonly FastApiConfig _fastApiConfig;
        private readonly RequestProcessor _requestProcessor;

        public FastApiServer(FastApiConfig fastApiConfig = null)
        {
            _fastApiConfig = fastApiConfig ?? ConfigurationShared.Instance.Get<FastApiConfig>();

            _listener = new HttpListener();
            _listener.Prefixes.Add(_fastApiConfig.UniformResourceLocator);
            _requestProcessor = new RequestProcessor(Route.Load());
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine("Server started...");
            while (true)
            {
                var context = _listener.GetContext();
                _requestProcessor.ProcessRequest(context);
            }
        }

        public void Stop()
        {
            _listener.Stop();
            Console.WriteLine("Server stopped...");
        }
    }
}