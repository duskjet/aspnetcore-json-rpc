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
using Microsoft.Extensions.DependencyInjection;
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
            var requestContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_req.json");
            var responseContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_res.json");

            var builder = new WebHostBuilder()
                .ConfigureLogging(lb => lb.SetMinimumLevel(LogLevel.Trace).AddXunit(_output));

            configurator.Invoke(builder);

            using (var server = new TestServer(builder))
            {
                using (var client = server.CreateClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestContent1 = new StringContent(requestContentSample);

                    requestContent1.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent1.Headers.ContentLength = requestContentSample.Length;

                    var response1 = await client.PostAsync("/api/v1", requestContent1).ConfigureAwait(false);

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

                    var requestContent2 = new StringContent(requestContentSample);

                    requestContent2.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent2.Headers.ContentEncoding.Add("gzip");

                    var response4 = await client.PostAsync("/api/v1", requestContent2).ConfigureAwait(false);

                    Assert.Equal(HttpStatusCode.BadRequest, response4.StatusCode);

                    var response2 = await client.PostAsync("/api/v2", requestContent1).ConfigureAwait(false);

                    Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);

                    var response3 = await client.PostAsync("/api/v1?p=v", requestContent1).ConfigureAwait(false);

                    Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
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
        [InlineData("ili")]
        [InlineData("bat")]
        [InlineData("bdi")]
        [InlineData("bsi")]
        [InlineData("bon")]
        [InlineData("ipt")]
        public async Task UseJsonRpcHandler(string test)
        {
            var options = new JsonRpcOptions
            {
                MaxBatchSize = 2,
                MaxIdLength = 36
            };

            void ConfigureMiddleware(IWebHostBuilder builder)
            {
                builder
                    .ConfigureServices(sc => sc.AddOptions().AddJsonRpcHandler<JsonRpcTestHandler>(options))
                    .Configure(ab => ab.UseJsonRpcHandler<JsonRpcTestHandler>("/api/v1"));
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
        [InlineData("ili")]
        [InlineData("bat")]
        [InlineData("bdi")]
        [InlineData("bsi")]
        [InlineData("bon")]
        [InlineData("ipt")]
        public async Task UseJsonRpcService(string test)
        {
            var options = new JsonRpcOptions
            {
                MaxBatchSize = 2,
                MaxIdLength = 36
            };

            void ConfigureMiddleware(IWebHostBuilder builder)
            {
                builder
                    .ConfigureServices(sc => sc.AddOptions().AddJsonRpcService<JsonRpcTestService>(options))
                    .Configure(ab => ab.UseJsonRpcService<JsonRpcTestService>("/api/v1"));
            }

            await InvokeMiddlewareTestAsync(ConfigureMiddleware, test);
        }
    }
}