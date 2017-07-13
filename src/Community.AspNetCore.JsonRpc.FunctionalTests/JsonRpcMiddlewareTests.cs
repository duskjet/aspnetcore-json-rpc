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
        public async Task HandlerWhenHttpPostRegular(string requestResource, string responseResource)
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
                var client = new HttpClient
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

        [Theory]
        [InlineData("Assets.operation_plus_request.json", "Assets.operation_plus_response.json")]
        [InlineData("Assets.operation_divide_1_request.json", "Assets.operation_divide_1_response.json")]
        [InlineData("Assets.operation_divide_2_request.json", "Assets.operation_divide_2_response.json")]
        [InlineData("Assets.operation_power_request.json", "Assets.operation_power_response.json")]
        [InlineData("Assets.invalid_method_request.json", "Assets.invalid_method_response.json")]
        [InlineData("Assets.invalid_message_request.json", "Assets.invalid_message_response.json")]
        public async Task ServiceWhenHttpPostRegular(string requestResource, string responseResource)
        {
            var requestContent = EmbeddedResourceManager.GetString(requestResource);
            var responseContentSample = EmbeddedResourceManager.GetString(responseResource);

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestService()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient
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

        [Theory]
        [InlineData("Assets.operation_ac_request.json")]
        public async Task HandlerWhenHttpPostNotification(string requestResource)
        {
            var requestContent = EmbeddedResourceManager.GetString(requestResource);

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (client)
                {
                    var response = await client.PostAsync("/service", new StringContent(requestContent, Encoding.UTF8, "application/json"));

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

                    var responseContent = await response.Content.ReadAsStringAsync();

                    Assert.Equal(string.Empty, responseContent);
                }
            }
        }

        [Theory]
        [InlineData("Assets.operation_ac_request.json")]
        public async Task ServiceWhenHttpPostNotification(string requestResource)
        {
            var requestContent = EmbeddedResourceManager.GetString(requestResource);

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestService()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (client)
                {
                    var response = await client.PostAsync("/service", new StringContent(requestContent, Encoding.UTF8, "application/json"));

                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

                    var responseContent = await response.Content.ReadAsStringAsync();

                    Assert.Equal(string.Empty, responseContent);
                }
            }
        }

        [Fact]
        public async Task HandlerWhenHttpGet()
        {
            var requestContent = EmbeddedResourceManager.GetString("Assets.get_request.json");

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient
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

                    Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
                }
            }
        }

        [Fact]
        public async Task HandlerWhenHttpPostWithInvalidAcceptHeader()
        {
            var requestContent = EmbeddedResourceManager.GetString("Assets.invalid_accept_request.json");

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/javascript"));

                using (client)
                {
                    var response = await client.PostAsync("/service", new StringContent(requestContent, Encoding.UTF8, "application/json"));

                    Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
                }
            }
        }

        [Fact]
        public async Task HandlerWhenHttpPostWithInvalidContentTypeHeader()
        {
            var requestContent = EmbeddedResourceManager.GetString("Assets.invalid_content_type_request.json");

            var server = new WebHostBuilder()
                .UseKestrel()
                .UseXunitLogger(_output)
                .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestHandler()))
                .Start("http://localhost:8080");

            using (server)
            {
                var client = new HttpClient
                {
                    BaseAddress = new Uri("http://localhost:8080")
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (client)
                {
                    var response = await client.PostAsync("/service", new StringContent(requestContent, Encoding.UTF8, "application/octet-stream"));

                    Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
                }
            }
        }

        [Fact]
        public void ServiceWhenMethodNamesAreNotUnique()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new WebHostBuilder()
                    .UseKestrel()
                    .UseXunitLogger(_output)
                    .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestServiceInvalidMethodName()))
                    .Start("http://localhost:8080"));
        }

        [Fact]
        public void ServiceWhenMethodReturnTypeIsInvalid()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new WebHostBuilder()
                    .UseKestrel()
                    .UseXunitLogger(_output)
                    .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestServiceInvalidMethodReturnType()))
                    .Start("http://localhost:8080"));
        }

        [Fact]
        public void ServiceWhenMethodParametersAreInvalid()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new WebHostBuilder()
                    .UseKestrel()
                    .UseXunitLogger(_output)
                    .Configure(app => app.UseJsonRpc("/service", new JsonRpcTestServiceInvalidMethodParameters()))
                    .Start("http://localhost:8080"));
        }
    }
}