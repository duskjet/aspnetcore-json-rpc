using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Tests.Middleware;
using Community.AspNetCore.JsonRpc.Tests.Resources;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Community.AspNetCore.JsonRpc.Tests
{
    public sealed class JsonRpcMiddlewareTests
    {
        private readonly ITestOutputHelper _output;

        public JsonRpcMiddlewareTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private async Task InvokeMiddlewareTestAsync(Action<IWebHostBuilder> configurator, string test)
        {
            void ConfigureLogging(ILoggingBuilder lb)
            {
                lb
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddXunit(_output);
            }

            var requestContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_req.json");
            var responseContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_res.json");

            var builder = new WebHostBuilder().ConfigureLogging(ConfigureLogging);

            configurator.Invoke(builder);

            using (var server = new TestServer(builder))
            {
                using (var client = server.CreateClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestContent = new StringContent(requestContentSample);

                    requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent.Headers.ContentLength = requestContentSample.Length;

                    var response1 = await client.PostAsync("/api/v1", requestContent).ConfigureAwait(false);

                    if (responseContentSample != string.Empty)
                    {
                        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

                        var responseContent = await response1.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.False(string.IsNullOrEmpty(responseContent), "Actual response content is empty");

                        var responseContentToken = JToken.Parse(responseContent);

                        _output.WriteLine(responseContentToken.ToString(Formatting.Indented));

                        Assert.True(JToken.DeepEquals(JToken.Parse(responseContentSample), responseContentToken), "Actual JSON string differs from expected");
                    }
                    else
                    {
                        Assert.Equal(HttpStatusCode.NoContent, response1.StatusCode);
                    }

                    var response2 = await client.PostAsync("/api/v2", requestContent).ConfigureAwait(false);

                    Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
                }
            }
        }

        [Theory]
        [InlineData("nam")]
        [InlineData("pos")]
        [InlineData("err")]
        [InlineData("not")]
        [InlineData("unk")]
        [InlineData("sys")]
        [InlineData("bat")]
        [InlineData("ipt")]
        public async Task UseJsonRpcHandler(string test)
        {
            void ConfigureMiddleware(IWebHostBuilder whb)
            {
                whb
                    .ConfigureServices(_ => _.AddJsonRpcHandler<JsonRpcTestHandler>())
                    .Configure(_ => _.UseJsonRpcHandler<JsonRpcTestHandler>("/api/v1"));
            }

            await InvokeMiddlewareTestAsync(ConfigureMiddleware, test);
        }

        [Theory]
        [InlineData("nam")]
        [InlineData("pos")]
        [InlineData("err")]
        [InlineData("not")]
        [InlineData("unk")]
        [InlineData("sys")]
        [InlineData("bat")]
        [InlineData("ipt")]
        public async Task UseJsonRpcService(string test)
        {
            void ConfigureMiddleware(IWebHostBuilder whb)
            {
                whb
                    .ConfigureServices(_ => _.AddJsonRpcService<JsonRpcTestService>())
                    .Configure(_ => _.UseJsonRpcService<JsonRpcTestService>("/api/v1"));
            }

            await InvokeMiddlewareTestAsync(ConfigureMiddleware, test);
        }
    }
}