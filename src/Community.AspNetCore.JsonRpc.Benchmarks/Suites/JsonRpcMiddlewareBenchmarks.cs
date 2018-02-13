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
        private readonly IDictionary<string, string> _resources = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly TestServer _serverHandler;
        private readonly TestServer _serverService;
        private readonly HttpClient _clientHandler;
        private readonly HttpClient _clientService;

        protected JsonRpcMiddlewareBenchmarks()
        {
            _serverHandler = new TestServer(ConfigureHandler(new WebHostBuilder()));
            _serverService = new TestServer(ConfigureService(new WebHostBuilder()));
            _clientHandler = _serverHandler.CreateClient();
            _clientService = _serverService.CreateClient();
            _clientHandler.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _clientService.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            foreach (var name in new[] { "nam", "pos", "err", "not" })
            {
                _resources[name] = EmbeddedResourceManager.GetString($"Assets.{name}.json");
            }
        }

        private static IWebHostBuilder ConfigureHandler(IWebHostBuilder builder)
        {
            return builder
                .ConfigureServices(sc => sc.AddJsonRpcService<JsonRpcTestService>())
                .Configure(ab => ab.UseJsonRpcService<JsonRpcTestService>());
        }

        private static IWebHostBuilder ConfigureService(IWebHostBuilder builder)
        {
            return builder
                .ConfigureServices(sc => sc.AddJsonRpcService<JsonRpcTestService>())
                .Configure(ab => ab.UseJsonRpcService<JsonRpcTestService>());
        }

        private HttpContent CreateHttpContent(string name)
        {
            var content = _resources[name];
            var result = new StringContent(content, Encoding.UTF8, "application/json");

            result.Headers.ContentLength = content.Length;

            return result;
        }

        [Benchmark(Description = "handler / nam")]
        public async Task HandlerWithParamsByName()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("nam")).ConfigureAwait(false);
        }

        [Benchmark(Description = "handler / pos")]
        public async Task HandlerWithParamsByPosition()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("pos")).ConfigureAwait(false);
        }

        [Benchmark(Description = "handler / err")]
        public async Task HandlerWithErrorResponse()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("err")).ConfigureAwait(false);
        }

        [Benchmark(Description = "handler / not")]
        public async Task HandlerWithNotification()
        {
            await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("not")).ConfigureAwait(false);
        }

        [Benchmark(Description = "service / nam")]
        public async Task ServiceWithParamsByName()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("nam")).ConfigureAwait(false);
        }

        [Benchmark(Description = "service / pos")]
        public async Task ServiceWithParamsByPosition()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("pos")).ConfigureAwait(false);
        }

        [Benchmark(Description = "service / err")]
        public async Task ServiceWithErrorResponse()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("err")).ConfigureAwait(false);
        }

        [Benchmark(Description = "service / not")]
        public async Task ServiceWithNotification()
        {
            await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("not")).ConfigureAwait(false);
        }
    }
}