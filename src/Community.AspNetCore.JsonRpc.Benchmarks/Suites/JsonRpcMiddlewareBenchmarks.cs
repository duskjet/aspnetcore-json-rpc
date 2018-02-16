using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Community.AspNetCore.JsonRpc.Benchmarks.Framework;
using Community.AspNetCore.JsonRpc.Benchmarks.Middleware;
using Community.AspNetCore.JsonRpc.Benchmarks.Resources;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Community.AspNetCore.JsonRpc.Benchmarks.Suites
{
    [BenchmarkSuite("JsonRpcMiddleware")]
    public abstract class JsonRpcMiddlewareBenchmarks
    {
        private static readonly MediaTypeHeaderValue _mimeType = new MediaTypeHeaderValue("aplication/json");
        private static readonly IReadOnlyDictionary<string, byte[]> _contents;

        private readonly TestServer _serverHandler;
        private readonly TestServer _serverService;
        private readonly HttpClient _clientHandler;
        private readonly HttpClient _clientService;

        static JsonRpcMiddlewareBenchmarks()
        {
            var contents = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var name in new[] { "nam", "pos", "err", "not" })
            {
                contents[name] = Encoding.UTF8.GetBytes(EmbeddedResourceManager.GetString($"Assets.{name}.json"));
            }

            _contents = contents;
        }

        protected JsonRpcMiddlewareBenchmarks()
        {
            var builderHandler = new WebHostBuilder()
                .ConfigureServices(sc => sc
                    .AddJsonRpcService<JsonRpcTestService>())
                .Configure(ab => ab
                    .UseJsonRpcService<JsonRpcTestService>());
            var builderService = new WebHostBuilder()
                .ConfigureServices(sc => sc
                    .AddJsonRpcService<JsonRpcTestService>())
                .Configure(ab => ab
                    .UseJsonRpcService<JsonRpcTestService>());

            _serverHandler = new TestServer(builderHandler);
            _serverService = new TestServer(builderService);
            _clientHandler = _serverHandler.CreateClient();
            _clientService = _serverService.CreateClient();
            _clientHandler.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _clientService.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private HttpContent CreateHttpContent(string name)
        {
            var content = _contents[name];
            var result = new ByteArrayContent(content);

            result.Headers.ContentType = _mimeType;
            result.Headers.ContentLength = content.Length;

            return result;
        }

        [Benchmark(Description = "handler / nam")]
        public async Task HandlerWithParamsByName()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("nam"));
        }

        [Benchmark(Description = "handler / pos")]
        public async Task HandlerWithParamsByPosition()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("pos"));
        }

        [Benchmark(Description = "handler / err")]
        public async Task HandlerWithErrorResponse()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("err"));
        }

        [Benchmark(Description = "handler / not")]
        public async Task HandlerWithNotification()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("not"));
        }

        [Benchmark(Description = "service / nam")]
        public async Task ServiceWithParamsByName()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("nam"));
        }

        [Benchmark(Description = "service / pos")]
        public async Task ServiceWithParamsByPosition()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("pos"));
        }

        [Benchmark(Description = "service / err")]
        public async Task ServiceWithErrorResponse()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("err"));
        }

        [Benchmark(Description = "service / not")]
        public async Task ServiceWithNotification()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("not"));
        }
    }
}