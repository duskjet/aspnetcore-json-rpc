// © Alexander Kozlenko. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.IO;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcMiddleware<T> : IMiddleware, IDisposable
        where T : class, IJsonRpcHandler
    {
        private readonly T _handler;
        private readonly JsonRpcSerializer _serializer;
        private readonly ILogger _logger;

        public JsonRpcMiddleware(IServiceProvider services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var handler = services.GetService<T>();
            var options = services.GetService<IOptions<JsonRpcOptions>>();
            var loggerFactory = services.GetService<ILoggerFactory>();

            _handler = handler ?? ActivatorUtilities.CreateInstance<T>(services);
            _serializer = new JsonRpcSerializer(CreateJsonRpcContractResolver(_handler), options?.Value?.JsonSerializer);
            _logger = loggerFactory?.CreateLogger<JsonRpcMiddleware<T>>();
        }

        private static JsonRpcContractResolver CreateJsonRpcContractResolver(T handler)
        {
            var contracts = handler.GetContracts();
            var resolver = new JsonRpcContractResolver();

            foreach (var kvp in contracts)
            {
                if (kvp.Key == null)
                {
                    throw new InvalidOperationException(Strings.GetString("handler.contract.method.undefined_name"));
                }
                if (JsonRpcSerializer.IsSystemMethod(kvp.Key))
                {
                    throw new InvalidOperationException(string.Format(Strings.GetString("handler.contract.method.system_name"), kvp.Key));
                }

                resolver.AddRequestContract(kvp.Key, kvp.Value);
            }

            return resolver;
        }

        async Task IMiddleware.InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Response.HasStarted)
            {
                return;
            }
            if (!string.Equals(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;

                return;
            }
            if (!string.Equals(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (context.Request.Headers.ContainsKey(HeaderNames.ContentEncoding))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (!string.Equals((string)context.Request.Headers[HeaderNames.Accept], "application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;

                return;
            }

            var jsonRpcRequestData = default(JsonRpcData<JsonRpcRequest>);

            try
            {
                jsonRpcRequestData = await _serializer.DeserializeRequestDataAsync(context.Request.Body, context.RequestAborted);
            }
            catch (JsonException e)
            {
                _logger?.LogError(4000, e, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                var jsonRpcError = new JsonRpcError(JsonRpcErrorCodes.InvalidJson, Strings.GetString("rpc.error.invalid_json"));
                var jsonRpcResponse = new JsonRpcResponse(jsonRpcError, default);

                context.Response.StatusCode = StatusCodes.Status200OK;

                await SerializeResponseAsync(context, jsonRpcResponse);

                return;
            }
            catch (JsonRpcException e)
            {
                _logger?.LogError(4000, e, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                var jsonRpcError = ConvertExceptionToError(e);
                var jsonRpcResponse = new JsonRpcResponse(jsonRpcError, default);

                context.Response.StatusCode = StatusCodes.Status200OK;

                await SerializeResponseAsync(context, jsonRpcResponse);

                return;
            }

            if (!jsonRpcRequestData.IsBatch)
            {
                var jsonRpcRequestItem = jsonRpcRequestData.Item;

                _logger?.LogDebug(1000, Strings.GetString("handler.request_data.accepted_single"), context.TraceIdentifier, context.Request.PathBase);

                context.RequestAborted.ThrowIfCancellationRequested();

                var jsonRpcResponse = await CreateJsonRpcResponseAsync(context, jsonRpcRequestItem);

                if (jsonRpcResponse == null)
                {
                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return;
                }
                if (jsonRpcRequestItem.IsValid && jsonRpcRequestItem.Message.IsNotification)
                {
                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return;
                }

                context.RequestAborted.ThrowIfCancellationRequested();
                context.Response.StatusCode = StatusCodes.Status200OK;

                await SerializeResponseAsync(context, jsonRpcResponse);
            }
            else
            {
                var jsonRpcRequestItems = jsonRpcRequestData.Items;

                _logger?.LogDebug(1010, Strings.GetString("handler.request_data.accepted_batch"), context.TraceIdentifier, jsonRpcRequestItems.Count, context.Request.PathBase);

                var jsonRpcRequestIdentifiers = new HashSet<JsonRpcId>();

                for (var i = 0; i < jsonRpcRequestItems.Count; i++)
                {
                    var jsonRpcRequestItem = jsonRpcRequestItems[i];

                    if (jsonRpcRequestItem.IsValid && !jsonRpcRequestItem.Message.IsNotification)
                    {
                        if (!jsonRpcRequestIdentifiers.Add(jsonRpcRequestItem.Message.Id))
                        {
                            _logger?.LogError(4020, Strings.GetString("handler.request_data.duplicate_ids"), context.TraceIdentifier);

                            var jsonRpcError = new JsonRpcError(-32000L, Strings.GetString("rpc.error.duplicate_ids"));
                            var jsonRpcResponse = new JsonRpcResponse(jsonRpcError, default);

                            context.Response.StatusCode = StatusCodes.Status200OK;

                            await SerializeResponseAsync(context, jsonRpcResponse);

                            return;
                        }
                    }
                }

                var jsonRpcResponses = new List<JsonRpcResponse>();

                for (var i = 0; i < jsonRpcRequestItems.Count; i++)
                {
                    context.RequestAborted.ThrowIfCancellationRequested();

                    var jsonRpcRequestItem = jsonRpcRequestItems[i];
                    var jsonRpcResponse = await CreateJsonRpcResponseAsync(context, jsonRpcRequestItem);

                    if (jsonRpcResponse != null)
                    {
                        if (jsonRpcRequestItem.IsValid && jsonRpcRequestItem.Message.IsNotification)
                        {
                            continue;
                        }

                        jsonRpcResponses.Add(jsonRpcResponse);
                    }
                }

                if (jsonRpcResponses.Count == 0)
                {
                    // Server must return empty content for empty response batch according to the JSON-RPC 2.0 specification

                    context.Response.StatusCode = StatusCodes.Status204NoContent;

                    return;
                }

                context.RequestAborted.ThrowIfCancellationRequested();
                context.Response.StatusCode = StatusCodes.Status200OK;

                await SerializeResponsesAsync(context, jsonRpcResponses);
            }
        }

        private async Task SerializeResponseAsync(HttpContext context, JsonRpcResponse jsonRpcResponse)
        {
            context.Response.ContentType = "application/json";

            using (var responseStream = new MemoryStream())
            {
                await _serializer.SerializeResponseAsync(jsonRpcResponse, responseStream, context.RequestAborted);

                context.Response.ContentLength = responseStream.Length;
                responseStream.Position = 0;

                await responseStream.CopyToAsync(context.Response.Body);
            }
        }

        private async Task SerializeResponsesAsync(HttpContext context, IReadOnlyList<JsonRpcResponse> jsonRpcResponses)
        {
            context.Response.ContentType = "application/json";

            using (var responseStream = new MemoryStream())
            {
                await _serializer.SerializeResponsesAsync(jsonRpcResponses, responseStream, context.RequestAborted);

                context.Response.ContentLength = responseStream.Length;
                responseStream.Position = 0;

                await responseStream.CopyToAsync(context.Response.Body);
            }
        }

        private async Task<JsonRpcResponse> CreateJsonRpcResponseAsync(HttpContext context, JsonRpcItem<JsonRpcRequest> requestItem)
        {
            if (!requestItem.IsValid)
            {
                var exception = requestItem.Exception;

                _logger?.LogError(4010, exception, Strings.GetString("handler.request.invalid_message"), context.TraceIdentifier, exception.MessageId);

                return new JsonRpcResponse(ConvertExceptionToError(exception), exception.MessageId);
            }

            var request = requestItem.Message;
            var response = await _handler.HandleAsync(request);

            if (response != null)
            {
                if (request.Id != response.Id)
                {
                    throw new InvalidOperationException(Strings.GetString("handler.response.invalid_id"));
                }

                if (!request.IsNotification)
                {
                    if (response.Success)
                    {
                        _logger?.LogInformation(2010, Strings.GetString("handler.response.handled_with_result"), context.TraceIdentifier, request.Id, request.Method);
                    }
                    else
                    {
                        _logger?.LogInformation(2020, Strings.GetString("handler.response.handled_with_error"), context.TraceIdentifier, request.Id, request.Method, response.Error.Code, response.Error.Message);
                    }
                }
                else
                {
                    if (response.Success)
                    {
                        _logger?.LogInformation(2030, Strings.GetString("handler.response.handled_with_result_as_notification"), context.TraceIdentifier, request.Method);
                    }
                    else
                    {
                        _logger?.LogInformation(2040, Strings.GetString("handler.response.handled_with_error_as_notification"), context.TraceIdentifier, request.Method, response.Error.Code, response.Error.Message);
                    }
                }
            }
            else
            {
                if (request.IsNotification)
                {
                    _logger?.LogInformation(2000, Strings.GetString("handler.response.handled_notification"), context.TraceIdentifier, request.Method);
                }
                else
                {
                    _logger?.LogWarning(3000, Strings.GetString("handler.response.handled_notification_as_response"), context.TraceIdentifier, request.Id, request.Method);
                }
            }

            return response;
        }

        private JsonRpcError ConvertExceptionToError(JsonRpcException exception)
        {
            var message = default(string);

            switch (exception.ErrorCode)
            {
                case JsonRpcErrorCodes.InvalidJson:
                    {
                        message = Strings.GetString("rpc.error.invalid_json");
                    }
                    break;
                case JsonRpcErrorCodes.InvalidParameters:
                    {
                        message = Strings.GetString("rpc.error.invalid_params");
                    }
                    break;
                case JsonRpcErrorCodes.InvalidMethod:
                    {
                        message = Strings.GetString("rpc.error.invalid_method");
                    }
                    break;
                case JsonRpcErrorCodes.InvalidMessage:
                    {
                        message = Strings.GetString("rpc.error.invalid_message");
                    }
                    break;
                default:
                    {
                        message = Strings.GetString("rpc.error.internal");
                    }
                    break;
            }

            return new JsonRpcError(exception.ErrorCode, message);
        }

        void IDisposable.Dispose()
        {
            (_handler as IDisposable)?.Dispose();
        }
    }
}