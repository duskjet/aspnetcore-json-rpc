using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.FunctionalTests.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Community.AspNetCore.JsonRpc.FunctionalTests
{
    public sealed class JsonRpcMiddlewareTests
    {
        private readonly ITestOutputHelper _output;

        public JsonRpcMiddlewareTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("Assets.operation_plus_request.json", "Assets.operation_plus_response.json")]
        [InlineData("Assets.operation_divide_1_request.json", "Assets.operation_divide_1_response.json")]
        [InlineData("Assets.operation_divide_2_request.json", "Assets.operation_divide_2_response.json")]
        [InlineData("Assets.operation_power_request.json", "Assets.operation_power_response.json")]
        [InlineData("Assets.invalid_method_request.json", "Assets.invalid_method_response.json")]
        [InlineData("Assets.invalid_message_request.json", "Assets.invalid_message_response.json")]
        [InlineData("Assets.invalid_processing_id_request.json", "Assets.invalid_processing_id_response.json")]
        [InlineData("Assets.invalid_processing_request.json", "Assets.invalid_processing_response.json")]
        public async Task Post(string requestResource, string responseResource)
        {
            var requestContent = EmbeddedResourceManager.GetString(requestResource);
            var responseContentSample = EmbeddedResourceManager.GetString(responseResource);

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient()
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (client)
                {
                    var response = await client.PostAsync("/service", new StringContent(requestContent, Encoding.UTF8, "application/json"));

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                    var responseContent = await response.Content.ReadAsStringAsync();

                    Assert.True(JToken.DeepEquals(JToken.Parse(responseContentSample), JToken.Parse(responseContent)));
                }
            }
        }

        [Fact]
        public async Task Get()
        {
            var requestContent = EmbeddedResourceManager.GetString("Assets.get_request.json");

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient()
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (client)
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, "/service")
                    {
                        Content = new StringContent(requestContent, Encoding.UTF8, "application/json")
                    };

                    var response = await client.SendAsync(message);

                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                }
            }
        }

        [Fact]
        public async Task PostWithInvalidAcceptHeader()
        {
            var requestContent = EmbeddedResourceManager.GetString("Assets.invalid_accept_request.json");

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient()
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/javascript"));

                using (client)
                {
                    var response = await client.PostAsync("/service", new StringContent(requestContent, Encoding.UTF8, "application/json"));

                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                }
            }
        }

        [Fact]
        public async Task PostWithInvalidContentTypeHeader()
        {
            var requestContent = EmbeddedResourceManager.GetString("Assets.invalid_content_type_request.json");

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient()
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (client)
                {
                    var response = await client.PostAsync("/service", new StringContent(requestContent, Encoding.UTF8, "application/javascript"));

                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                }
            }
        }
    }
}