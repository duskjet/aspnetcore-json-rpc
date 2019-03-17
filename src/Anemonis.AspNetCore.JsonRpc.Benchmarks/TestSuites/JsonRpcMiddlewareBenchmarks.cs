using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Anemonis.AspNetCore.JsonRpc.Benchmarks.Resources;
using Anemonis.AspNetCore.JsonRpc.Benchmarks.TestStubs;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Anemonis.AspNetCore.JsonRpc.Benchmarks.TestSuites
{
    public class JsonRpcMiddlewareBenchmarks
    {
        private static readonly IReadOnlyDictionary<string, byte[]> _resources = CreateResourceDictionary();
        private static readonly MediaTypeHeaderValue _mimeType = new MediaTypeHeaderValue("application/json");

        private readonly TestServer _server;
        private readonly HttpClient _client;

        private static IReadOnlyDictionary<string, byte[]> CreateResourceDictionary()
        {
            var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var code in CreateTestCodes())
            {
                resources[code] = Encoding.UTF8.GetBytes(EmbeddedResourceManager.GetString($"Assets.{code}.json"));
            }

            return resources;
        }

        private static IEnumerable<string> CreateTestCodes()
        {
            return new[]
            {
                "b0t0p0e0d0", "b0t0p0e1d0", "b0t0p0e1d1",
                "b0t0p1e0d0", "b0t0p1e1d0", "b0t0p1e1d1",
                "b0t0p2e0d0", "b0t0p2e1d0", "b0t0p2e1d1",
                "b0t1p0e0d0", "b0t1p0e1d0", "b0t1p0e1d1",
                "b0t1p1e0d0", "b0t1p1e1d0", "b0t1p1e1d1",
                "b0t1p2e0d0", "b0t1p2e1d0", "b0t1p2e1d1",
                "b1t0p0e0d0", "b1t0p0e1d0", "b1t0p0e1d1",
                "b1t0p1e0d0", "b1t0p1e1d0", "b1t0p1e1d1",
                "b1t0p2e0d0", "b1t0p2e1d0", "b1t0p2e1d1",
                "b1t1p0e0d0", "b1t1p0e1d0", "b1t1p0e1d1",
                "b1t1p1e0d0", "b1t1p1e1d0", "b1t1p1e1d1",
                "b1t1p2e0d0", "b1t1p2e1d0", "b1t1p2e1d1"
            };
        }

        public JsonRpcMiddlewareBenchmarks()
        {
            var builderHandler = new WebHostBuilder()
                .ConfigureServices(sc => sc.AddJsonRpcService<JsonRpcTestService>())
                .Configure(ab => ab.UseJsonRpcService<JsonRpcTestService>("/api/v1"));
            var builderService = new WebHostBuilder()
                .ConfigureServices(sc => sc.AddJsonRpcService<JsonRpcTestService>())
                .Configure(ab => ab.UseJsonRpcService<JsonRpcTestService>("/api/v2"));

            _server = new TestServer(builderHandler);
            _client = _server.CreateClient();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private HttpContent CreateHttpContent(string name)
        {
            var content = _resources[name];
            var result = new ByteArrayContent(content);

            result.Headers.ContentType = _mimeType;
            result.Headers.ContentLength = content.Length;

            return result;
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P0E0D0")]
        public async Task<object> HandlerB0T0P0E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p0e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P0E1D0")]
        public async Task<object> HandlerB0T0P0E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p0e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P0E1D1")]
        public async Task<object> HandlerB0T0P0E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p0e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P1E0D0")]
        public async Task<object> HandlerB0T0P1E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p1e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P1E1D0")]
        public async Task<object> HandlerB0T0P1E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p1e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P1E1D1")]
        public async Task<object> HandlerB0T0P1E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p1e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P2E0D0")]
        public async Task<object> HandlerB0T0P2E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p2e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P2E1D0")]
        public async Task<object> HandlerB0T0P2E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p2e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T0P2E1D1")]
        public async Task<object> HandlerB0T0P2E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t0p2e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P0E0D0")]
        public async Task<object> HandlerB0T1P0E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p0e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P0E1D0")]
        public async Task<object> HandlerB0T1P0E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p0e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P0E1D1")]
        public async Task<object> HandlerB0T1P0E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p0e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P1E0D0")]
        public async Task<object> HandlerB0T1P1E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p1e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P1E1D0")]
        public async Task<object> HandlerB0T1P1E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p1e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P1E1D1")]
        public async Task<object> HandlerB0T1P1E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p1e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P2E0D0")]
        public async Task<object> HandlerB0T1P2E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p2e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P2E1D0")]
        public async Task<object> HandlerB0T1P2E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p2e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B0T1P2E1D1")]
        public async Task<object> HandlerB0T1P2E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b0t1p2e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P0E0D0")]
        public async Task<object> HandlerB1T0P0E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p0e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P0E1D0")]
        public async Task<object> HandlerB1T0P0E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p0e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P0E1D1")]
        public async Task<object> HandlerB1T0P0E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p0e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P1E0D0")]
        public async Task<object> HandlerB1T0P1E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p1e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P1E1D0")]
        public async Task<object> HandlerB1T0P1E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p1e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P1E1D1")]
        public async Task<object> HandlerB1T0P1E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p1e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P2E0D0")]
        public async Task<object> HandlerB1T0P2E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p2e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P2E1D0")]
        public async Task<object> HandlerB1T0P2E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p2e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T0P2E1D1")]
        public async Task<object> HandlerB1T0P2E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t0p2e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P0E0D0")]
        public async Task<object> HandlerB1T1P0E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p0e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P0E1D0")]
        public async Task<object> HandlerB1T1P0E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p0e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P0E1D1")]
        public async Task<object> HandlerB1T1P0E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p0e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P1E0D0")]
        public async Task<object> HandlerB1T1P1E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p1e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P1E1D0")]
        public async Task<object> HandlerB1T1P1E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p1e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P1E1D1")]
        public async Task<object> HandlerB1T1P1E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p1e1d1"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P2E0D0")]
        public async Task<object> HandlerB1T1P2E0D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p2e0d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P2E1D0")]
        public async Task<object> HandlerB1T1P2E1D0()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p2e1d0"));
        }

        [Benchmark(Description = "TYPE=H-CASE=B1T1P2E1D1")]
        public async Task<object> HandlerB1T1P2E1D1()
        {
            return await _client.PostAsync("/api/v1", CreateHttpContent("b1t1p2e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P0E0D0")]
        public async Task<object> ServiceB0T0P0E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p0e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P0E1D0")]
        public async Task<object> ServiceB0T0P0E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p0e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P0E1D1")]
        public async Task<object> ServiceB0T0P0E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p0e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P1E0D0")]
        public async Task<object> ServiceB0T0P1E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p1e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P1E1D0")]
        public async Task<object> ServiceB0T0P1E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p1e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P1E1D1")]
        public async Task<object> ServiceB0T0P1E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p1e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P2E0D0")]
        public async Task<object> ServiceB0T0P2E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p2e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P2E1D0")]
        public async Task<object> ServiceB0T0P2E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p2e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T0P2E1D1")]
        public async Task<object> ServiceB0T0P2E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t0p2e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P0E0D0")]
        public async Task<object> ServiceB0T1P0E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p0e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P0E1D0")]
        public async Task<object> ServiceB0T1P0E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p0e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P0E1D1")]
        public async Task<object> ServiceB0T1P0E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p0e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P1E0D0")]
        public async Task<object> ServiceB0T1P1E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p1e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P1E1D0")]
        public async Task<object> ServiceB0T1P1E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p1e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P1E1D1")]
        public async Task<object> ServiceB0T1P1E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p1e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P2E0D0")]
        public async Task<object> ServiceB0T1P2E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p2e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P2E1D0")]
        public async Task<object> ServiceB0T1P2E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p2e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B0T1P2E1D1")]
        public async Task<object> ServiceB0T1P2E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b0t1p2e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P0E0D0")]
        public async Task<object> ServiceB1T0P0E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p0e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P0E1D0")]
        public async Task<object> ServiceB1T0P0E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p0e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P0E1D1")]
        public async Task<object> ServiceB1T0P0E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p0e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P1E0D0")]
        public async Task<object> ServiceB1T0P1E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p1e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P1E1D0")]
        public async Task<object> ServiceB1T0P1E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p1e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P1E1D1")]
        public async Task<object> ServiceB1T0P1E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p1e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P2E0D0")]
        public async Task<object> ServiceB1T0P2E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p2e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P2E1D0")]
        public async Task<object> ServiceB1T0P2E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p2e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T0P2E1D1")]
        public async Task<object> ServiceB1T0P2E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t0p2e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P0E0D0")]
        public async Task<object> ServiceB1T1P0E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p0e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P0E1D0")]
        public async Task<object> ServiceB1T1P0E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p0e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P0E1D1")]
        public async Task<object> ServiceB1T1P0E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p0e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P1E0D0")]
        public async Task<object> ServiceB1T1P1E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p1e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P1E1D0")]
        public async Task<object> ServiceB1T1P1E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p1e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P1E1D1")]
        public async Task<object> ServiceB1T1P1E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p1e1d1"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P2E0D0")]
        public async Task<object> ServiceB1T1P2E0D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p2e0d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P2E1D0")]
        public async Task<object> ServiceB1T1P2E1D0()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p2e1d0"));
        }

        [Benchmark(Description = "TYPE=S-CASE=B1T1P2E1D1")]
        public async Task<object> ServiceB1T1P2E1D1()
        {
            return await _client.PostAsync("/api/v2", CreateHttpContent("b1t1p2e1d1"));
        }

    }
}