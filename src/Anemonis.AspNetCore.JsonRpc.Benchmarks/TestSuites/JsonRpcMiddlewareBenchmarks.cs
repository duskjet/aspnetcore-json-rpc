using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Anemonis.AspNetCore.JsonRpc.Benchmarks.Middleware;
using Anemonis.AspNetCore.JsonRpc.Benchmarks.Resources;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Anemonis.AspNetCore.JsonRpc.Benchmarks.TestSuites
{
    public sealed class JsonRpcMiddlewareBenchmarks
    {
        private static readonly IReadOnlyDictionary<string, byte[]> _resources = CreateResourceDictionary();
        private static readonly MediaTypeHeaderValue _mimeType = new MediaTypeHeaderValue("application/json");

        private readonly TestServer _serverHandler;
        private readonly TestServer _serverService;
        private readonly HttpClient _clientHandler;
        private readonly HttpClient _clientService;

        private static IReadOnlyDictionary<string, byte[]> CreateResourceDictionary()
        {
            var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var code in CreateRequestcodes())
            {
                resources[code] = Encoding.UTF8.GetBytes(EmbeddedResourceManager.GetString($"Assets.{code}.json"));
            }

            return resources;
        }

        private static IEnumerable<string> CreateRequestcodes()
        {
            return new[] { "nam", "pos", "err", "not" };
        }

        public JsonRpcMiddlewareBenchmarks()
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
            var content = _resources[name];
            var result = new ByteArrayContent(content);

            result.Headers.ContentType = _mimeType;
            result.Headers.ContentLength = content.Length;

            return result;
        }

        [Benchmark(Description = "TYPE=H-ID=N-PARAMS=U-ERROR=N")]
        public async Task<object> HandlerWithNotification()
        {
            return await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("not"));
        }

        [Benchmark(Description = "TYPE=H-ID=Y-PARAMS=N-ERROR=N")]
        public async Task<object> HandlerWithParametersByName()
        {
            return await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("nam"));
        }

        [Benchmark(Description = "TYPE=H-ID=Y-PARAMS=P-ERROR=N")]
        public async Task<object> HandlerWithParametersByPosition()
        {
            return await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("pos"));
        }

        [Benchmark(Description = "TYPE=H-ID=Y-PARAMS=U-ERROR=Y")]
        public async Task<object> HandlerWithErrorResponse()
        {
            return await _clientHandler.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("err"));
        }

        [Benchmark(Description = "TYPE=S-ID=N-PARAMS=U-ERROR=N")]
        public async Task<object> ServiceWithNotification()
        {
            return await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("not"));
        }

        [Benchmark(Description = "TYPE=S-ID=Y-PARAMS=N-ERROR=N")]
        public async Task<object> ServiceWithParametersByName()
        {
            return await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("nam"));
        }

        [Benchmark(Description = "TYPE=S-ID=Y-PARAMS=P-ERROR=N")]
        public async Task<object> ServiceWithParametersByPosition()
        {
            return await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("pos"));
        }

        [Benchmark(Description = "TYPE=S-ID=Y-PARAMS=U-ERROR=Y")]
        public async Task<object> ServiceWithErrorResponse()
        {
            return await _clientService.PostAsync(_serverHandler.BaseAddress, CreateHttpContent("err"));
        }
    }
}