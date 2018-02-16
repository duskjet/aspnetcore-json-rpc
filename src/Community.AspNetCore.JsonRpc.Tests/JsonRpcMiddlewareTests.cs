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

        private async Task ExecuteMiddlewareTestAsync(Action<IWebHostBuilder> configurator, string test)
        {
            var requestContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_req.json");
            var responseContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_res.json");

            var builder = new WebHostBuilder()
                .ConfigureLogging(lb => lb
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddXunit(_output));

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
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                        var responseContent = await response.Content.ReadAsStringAsync();

                        Assert.False(string.IsNullOrEmpty(responseContent), "Actual response content is empty");

                        var responseContentToken = JToken.Parse(responseContent);

                        _output.WriteLine(responseContentToken.ToString(Formatting.Indented));

                        Assert.True(JToken.DeepEquals(JToken.Parse(responseContentSample), responseContentToken), "Actual JSON string differs from expected");
                    }
                    else
                    {
                        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    }
                }
            }
        }

        private async Task ExecuteMiddlewareTestWithInvalidResponseAsync(Action<IWebHostBuilder> configurator)
        {
            var builder = new WebHostBuilder()
                .ConfigureLogging(lb => lb
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddXunit(_output));

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

                    Assert.Equal(HttpStatusCode.NotFound, response1.StatusCode);

                    var requestContent2 = new StringContent("");

                    requestContent2.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent2.Headers.ContentLength = 0L;

                    var response2 = await client.PostAsync("/api/v1?p=v", requestContent2);

                    Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);

                    var requestContent3 = new StringContent("");

                    requestContent3.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent3.Headers.ContentLength = 0L;
                    requestContent3.Headers.ContentEncoding.Add("deflate");

                    var response3 = await client.PostAsync("/api/v1", requestContent3);

                    Assert.Equal(HttpStatusCode.UnsupportedMediaType, response3.StatusCode);

                    var requestContent4 = new StringContent("");

                    requestContent4.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    requestContent4.Headers.ContentLength = 0L;
                    requestContent4.Headers.ContentEncoding.Add("identity");

                    var response4 = await client.PostAsync("/api/v1", requestContent4);

                    Assert.Equal(HttpStatusCode.OK, response4.StatusCode);
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

        [Fact]
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

        [Fact]
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