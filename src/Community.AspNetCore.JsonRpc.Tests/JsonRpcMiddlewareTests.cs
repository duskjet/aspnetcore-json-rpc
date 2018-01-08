using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Tests.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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

        [Theory]
        [InlineData("pin")]
        [InlineData("clr")]
        [InlineData("add")]
        [InlineData("sub")]
        public async Task Handler(string test)
        {
            var requestContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_req.json");
            var responseContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_res.json");

            var builder = new WebHostBuilder().UseXunitLogger(_output).Configure(app => app.UseJsonRpcHandler<JsonRpcTestHandler>());

            using (var server = new TestServer(builder))
            {
                using (var client = server.CreateClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestContent = new StringContent(requestContentSample, Encoding.UTF8, "application/json");

                    requestContent.Headers.ContentLength = requestContentSample.Length;

                    var response = await client.PostAsync("/", requestContent).ConfigureAwait(false);

                    if (responseContentSample != string.Empty)
                    {
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.True(JToken.DeepEquals(JToken.Parse(responseContentSample), JToken.Parse(responseContent)));
                    }
                    else
                    {
                        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    }
                }
            }
        }

        [Theory]
        [InlineData("pin")]
        [InlineData("clr")]
        [InlineData("add")]
        [InlineData("sub")]
        public async Task Service(string test)
        {
            var requestContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_req.json");
            var responseContentSample = EmbeddedResourceManager.GetString($"Assets.{test}_res.json");

            var builder = new WebHostBuilder().UseXunitLogger(_output).Configure(app => app.UseJsonRpcService<JsonRpcTestService>());

            using (var server = new TestServer(builder))
            {
                using (var client = server.CreateClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestContent = new StringContent(requestContentSample, Encoding.UTF8, "application/json");

                    requestContent.Headers.ContentLength = requestContentSample.Length;

                    var response = await client.PostAsync("/", requestContent).ConfigureAwait(false);

                    if (responseContentSample != string.Empty)
                    {
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        Assert.True(JToken.DeepEquals(JToken.Parse(responseContentSample), JToken.Parse(responseContent)));
                    }
                    else
                    {
                        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    }
                }
            }
        }
    }
}