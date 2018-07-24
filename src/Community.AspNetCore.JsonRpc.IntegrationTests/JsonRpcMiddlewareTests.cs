using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.IntegrationTests.Middleware;
using Community.AspNetCore.JsonRpc.IntegrationTests.Resources;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Community.AspNetCore.JsonRpc.IntegrationTests
{
    [TestClass]
    public sealed class JsonRpcMiddlewareTests
    {
        [Conditional("DEBUG")]
        private static void TraceJsonToken(JToken token)
        {
            Trace.WriteLine(token.ToString(Formatting.Indented));
        }

        private async Task ExecuteMiddlewareTestAsync(Action<IWebHostBuilder> configurator, string test)
        {
            var requestContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_req.json");
            var responseContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_res.json");

            var builder = new WebHostBuilder()
                .ConfigureLogging(lb => lb
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddDebug());

            configurator.Invoke(builder);

            using (var server = new TestServer(builder))
            {
                using (var client = server.CreateClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestContent = new StringContent(requestContentSample);

                    requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent.Headers.ContentLength = requestContentSample.Length;

                    var response = await client.PostAsync("/api/v1", requestContent);

                    if (responseContentSample != string.Empty)
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                        var responseContent = await response.Content.ReadAsStringAsync();

                        Assert.IsFalse(string.IsNullOrEmpty(responseContent), "Actual response content is empty");

                        var responseContentToken = JToken.Parse(responseContent);

                        TraceJsonToken(responseContentToken);

                        Assert.IsTrue(JToken.DeepEquals(JToken.Parse(responseContentSample), responseContentToken), "Actual JSON string differs from expected");
                    }
                    else
                    {
                        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                    }
                }
            }
        }

        private async Task ExecuteMiddlewareTestWithInvalidResponseAsync(Action<IWebHostBuilder> configurator)
        {
            var builder = new WebHostBuilder()
                .ConfigureLogging(lb => lb
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddDebug());

            configurator.Invoke(builder);

            using (var server = new TestServer(builder))
            {
                using (var client = server.CreateClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestContent1 = new StringContent("");

                    requestContent1.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent1.Headers.ContentLength = 0L;

                    var response1 = await client.PostAsync("/api/v2", requestContent1);

                    Assert.AreEqual(HttpStatusCode.NotFound, response1.StatusCode);

                    var requestContent2 = new StringContent("");

                    requestContent2.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent2.Headers.ContentLength = 0L;
                    requestContent2.Headers.ContentEncoding.Add("deflate");

                    var response2 = await client.PostAsync("/api/v1", requestContent2);

                    Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, response2.StatusCode);
                }
            }
        }

        [DataTestMethod]
        [DataRow("nam")]
        [DataRow("pos")]
        [DataRow("err")]
        [DataRow("not")]
        [DataRow("unk")]
        [DataRow("sys")]
        [DataRow("ili")]
        [DataRow("bat")]
        [DataRow("bdi")]
        [DataRow("bsi")]
        [DataRow("bon")]
        [DataRow("ipt")]
        public async Task UseJsonRpcHandler(string test)
        {
            var options = new JsonRpcOptions
            {
                MaxBatchSize = 2,
                MaxIdLength = 36
            };

            var configurator = (Action<IWebHostBuilder>)(builder =>
            {
                builder
                    .ConfigureServices(sc => sc
                        .AddJsonRpcHandler<JsonRpcTestHandler>(options))
                    .Configure(ab => ab
                        .UseJsonRpcHandler<JsonRpcTestHandler>("/api/v1"));
            });

            await ExecuteMiddlewareTestAsync(configurator, test);
        }

        [DataTestMethod]
        [DataRow("nam")]
        [DataRow("pos")]
        [DataRow("err")]
        [DataRow("not")]
        [DataRow("unk")]
        [DataRow("sys")]
        [DataRow("ili")]
        [DataRow("bat")]
        [DataRow("bdi")]
        [DataRow("bsi")]
        [DataRow("bon")]
        [DataRow("ipt")]
        public async Task UseJsonRpcService(string test)
        {
            var options = new JsonRpcOptions
            {
                MaxBatchSize = 2,
                MaxIdLength = 36
            };

            var configurator = (Action<IWebHostBuilder>)(builder =>
            {
                builder
                    .ConfigureServices(sc => sc
                        .AddJsonRpcService<JsonRpcTestService>(options))
                    .Configure(ab => ab
                        .UseJsonRpcService<JsonRpcTestService>("/api/v1"));
            });

            await ExecuteMiddlewareTestAsync(configurator, test);
        }

        [TestMethod]
        public async Task UseJsonRpcHandlerWithInvalidResponse()
        {
            var configurator = (Action<IWebHostBuilder>)(builder =>
            {
                builder
                    .ConfigureServices(sc => sc
                        .AddJsonRpcHandler<JsonRpcTestHandler>())
                    .Configure(ab => ab
                        .UseJsonRpcHandler<JsonRpcTestHandler>("/api/v1"));
            });

            await ExecuteMiddlewareTestWithInvalidResponseAsync(configurator);
        }

        [TestMethod]
        public async Task UseJsonRpcServiceWithInvalidResponse()
        {
            var configurator = (Action<IWebHostBuilder>)(builder =>
            {
                builder
                    .ConfigureServices(sc => sc
                        .AddJsonRpcService<JsonRpcTestService>())
                    .Configure(ab => ab
                        .UseJsonRpcService<JsonRpcTestService>("/api/v1"));
            });

            await ExecuteMiddlewareTestWithInvalidResponseAsync(configurator);
        }
    }
}