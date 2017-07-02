using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcMiddleware
    {
        private static readonly JsonRpcError _jsonRpcErrorInternal = new JsonRpcError(-32603L, "Internal JSON-RPC error");
        private static readonly MediaTypeHeaderValue _mediaType = new MediaTypeHeaderValue("application/json");

        private readonly IJsonRpcHandler _handler;
        private readonly ILogger _logger;
        private readonly JsonRpcSerializer _serializer;

        public JsonRpcMiddleware(RequestDelegate next, IJsonRpcHandler handler, ILoggerFactory loggerFactory)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _handler = handler;
            _mediaType.CharSet = Encoding.UTF8.WebName;
            _serializer = CreateSerializer(handler.CreateScheme());
            _logger = loggerFactory.CreateLogger<JsonRpcMiddleware>();
        }

        private static JsonRpcSerializer CreateSerializer(JsonRpcSerializerScheme scheme)
        {
            var settings = new JsonRpcSerializerSettings
            {
                JsonSerializerArrayPool = new JsonRpcArrayPool()
            };

            return new JsonRpcSerializer(scheme, settings);
        }

        private static JsonRpcError ConvertToError(JsonRpcException exception)
        {
            switch (exception.Type)
            {
                case JsonRpcExceptionType.InvalidMethod:
                    {
                        return new JsonRpcError(-32601L, exception.Message);
                    }
                case JsonRpcExceptionType.InvalidMessage:
                    {
                        return new JsonRpcError(-32600L, exception.Message);
                    }
                case JsonRpcExceptionType.ParseError:
                    {
                        return new JsonRpcError(-32700L, exception.Message);
                    }
                default:
                    {
                        return _jsonRpcErrorInternal;
                    }
            }
        }

        private static bool ValidateMediaType(string value)
        {
            return MediaTypeHeaderValue.TryParse(value, out var result) && (string.Compare(result.MediaType, _mediaType.MediaType, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private async Task<JsonRpcResponse> ConvertToResponse(JsonRpcItem<JsonRpcRequest> item)
        {
            if (item.IsValid)
            {
                var jsonRpcRequest = item.GetMessage();
                var jsonRpcResponse = default(JsonRpcResponse);

                try
                {
                    if (!jsonRpcRequest.IsNotification)
                    {
                        jsonRpcResponse = await _handler.HandleRequest(jsonRpcRequest).ConfigureAwait(false);
                    }
                    else
                    {
                        await _handler.HandleNotification(jsonRpcRequest).ConfigureAwait(false);
                    }
                }
                catch (JsonRpcException ex)
                {
                    _logger.LogTrace(1, ex, "Unknown \"{RequestId}\" request processing error: {ExceptionType}", jsonRpcRequest.Id, ex.Type);

                    return !jsonRpcRequest.IsNotification ? new JsonRpcResponse(_jsonRpcErrorInternal, jsonRpcRequest.Id) : default(JsonRpcResponse);
                }

                if (!jsonRpcRequest.IsNotification)
                {
                    if (jsonRpcResponse == null)
                    {
                        _logger.LogTrace(2, "Response for the \"{RequestId}\" is (null)", jsonRpcRequest.Id);

                        return new JsonRpcResponse(_jsonRpcErrorInternal, jsonRpcRequest.Id);
                    }
                    if (jsonRpcRequest.Id != jsonRpcResponse.Id)
                    {
                        _logger.LogTrace(2, "Response for the \"{RequestId}\" request has invalid identifier: \"{ResponseId}\"", jsonRpcRequest.Id, jsonRpcResponse.Id);

                        return new JsonRpcResponse(_jsonRpcErrorInternal, jsonRpcRequest.Id);
                    }

                    return jsonRpcResponse;
                }
                else
                {
                    return default(JsonRpcResponse);
                }
            }
            else
            {
                var exception = item.GetException();

                return new JsonRpcResponse(ConvertToError(exception), exception.MessageId);
            }
        }

        public async Task Invoke(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.Compare(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) != 0)
            {
                _logger.LogTrace(0, "Unsupported HTTP method: \"{Method}\"", context.Request.Method);

                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else if (!ValidateMediaType(context.Request.ContentType))
            {
                _logger.LogTrace(0, "Unsupported request media type: \"{Type}\"", context.Request.ContentType);

                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else if (!ValidateMediaType(context.Request.Headers["Accept"]))
            {
                _logger.LogTrace(0, "Unsupported response media type: \"{Type}\"", context.Request.Headers["Accept"]);

                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else
            {
                _logger.LogInformation(0, "Processing a JSON-RPC 2.0 request to the \"{Path}\" endpoint from the \"{Address}\" address", context.Request.PathBase, context.Connection.RemoteIpAddress);

                var jsonRpcRequestData = default(JsonRpcData<JsonRpcRequest>);
                var responseString = string.Empty;

                try
                {
                    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                    {
                        jsonRpcRequestData = _serializer.DeserializeRequestsData(await reader.ReadToEndAsync().ConfigureAwait(false));
                    }
                }
                catch (JsonRpcException ex)
                {
                    _logger.LogTrace(1, ex, "Unknown JSON-RPC request processing error");

                    responseString = _serializer.SerializeResponse(new JsonRpcResponse(ConvertToError(ex), JsonRpcId.None));
                }

                if (jsonRpcRequestData != null)
                {
                    if (!jsonRpcRequestData.IsBatch)
                    {
                        var jsonRpcResponse = await ConvertToResponse(jsonRpcRequestData.GetSingleItem()).ConfigureAwait(false);

                        if (jsonRpcResponse != default(JsonRpcResponse))
                        {
                            responseString = _serializer.SerializeResponse(jsonRpcResponse);
                        }
                    }
                    else
                    {
                        var items = jsonRpcRequestData.GetBatchItems();
                        var jsonRpcResponses = new List<JsonRpcResponse>(items.Count);

                        for (var i = 0; i < items.Count; i++)
                        {
                            var jsonRpcResponse = await ConvertToResponse(items[i]).ConfigureAwait(false);

                            if (jsonRpcResponse != default(JsonRpcResponse))
                            {
                                jsonRpcResponses.Add(jsonRpcResponse);
                            }
                        }

                        responseString = _serializer.SerializeResponses(jsonRpcResponses);
                    }
                }

                var responseBytes = Encoding.UTF8.GetBytes(responseString);

                context.Response.ContentType = _mediaType.ToString();
                context.Response.ContentLength = responseBytes.Length;

                await context.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length).ConfigureAwait(false);
            }
        }
    }
}