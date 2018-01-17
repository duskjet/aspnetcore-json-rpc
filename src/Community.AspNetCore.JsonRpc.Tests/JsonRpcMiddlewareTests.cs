using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Tests.Middleware;
using Community.AspNetCore.JsonRpc.Tests.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
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

        private async Task TestMiddlewareAsync(Action<IApplicationBuilder> action, string test)
        {
            var requestContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_req.json");
            var responseContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_res.json");

            var builder = new WebHostBuilder().ConfigureLogging(_ => _.AddXunit(_output)).Configure(action);

            using (var server = new TestServer(builder))
            {
                using (var client = server.CreateClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestContent = new StringContent(requestContentSample, Encoding.UTF8, "application/json");

                    requestContent.Headers.ContentLength = requestContentSample.Length;

                    var response = await client.PostAsync(server.BaseAddress, requestContent).ConfigureAwait(false);

                    if (responseContentSample != string.Empty)
                    {
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.True(JToken.DeepEquals(JToken.Parse(responseContentSample), JToken.Parse(responseContent)), "Actual JSON string differs from expected");
                    }
                    else
                    {
                        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    }
                }
            }
        }

        [Theory]
        [InlineData("nam")]
        [InlineData("pos")]
        [InlineData("err")]
        [InlineData("not")]
        public async Task JsonRpcHandler(string test)
        {
            await TestMiddlewareAsync(_ => _.UseJsonRpcHandler<JsonRpcTestHandler>(), test);
        }

        [Theory]
        [InlineData("nam")]
        [InlineData("pos")]
        [InlineData("err")]
        [InlineData("not")]
        public async Task JsonRpcService(string test)
        {
            await TestMiddlewareAsync(_ => _.UseJsonRpcService<JsonRpcTestService>(), test);
        }
    }
}