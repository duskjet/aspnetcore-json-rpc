using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
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
        private static readonly ResourceManager _resourceManager = CreateResourceManager();

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValidateMediaType(string value)
        {
            return MediaTypeHeaderValue.TryParse(value, out var result) && (string.Compare(result.MediaType, _mediaType.MediaType, StringComparison.OrdinalIgnoreCase) == 0);
        }

        private async Task<JsonRpcResponse> ProcessRequest(JsonRpcItem<JsonRpcRequest> item)
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
                    _logger.LogTrace(1, ex, _resourceManager.GetString("RequestProcessingError"), jsonRpcRequest.Id, ex.Type);

                    return !jsonRpcRequest.IsNotification ? new JsonRpcResponse(_jsonRpcErrorInternal, jsonRpcRequest.Id) : default(JsonRpcResponse);
                }

                if (!jsonRpcRequest.IsNotification)
                {
                    if (jsonRpcResponse == null)
                    {
                        _logger.LogTrace(2, _resourceManager.GetString("ResponseIsUndefined"), jsonRpcRequest.Id);

                        return new JsonRpcResponse(_jsonRpcErrorInternal, jsonRpcRequest.Id);
                    }
                    if (jsonRpcRequest.Id != jsonRpcResponse.Id)
                    {
                        _logger.LogTrace(2, _resourceManager.GetString("ResponseHasInvalidIdentifier"), jsonRpcRequest.Id, jsonRpcResponse.Id);

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

            if (string.Compare(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase) != 0)
            {
                _logger.LogTrace(0, _resourceManager.GetString("UnsupportedHttpMethod"), context.Request.Method);

                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else if (!ValidateMediaType(context.Request.ContentType))
            {
                _logger.LogTrace(0, _resourceManager.GetString("UnsupportedContentTypeHeader"), context.Request.ContentType);

                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
            }
            else if (!ValidateMediaType(context.Request.Headers["Accept"]))
            {
                _logger.LogTrace(0, _resourceManager.GetString("UnsupportedAcceptHeader"), context.Request.Headers["Accept"]);

                context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
            }
            else if (!context.Request.ContentLength.HasValue)
            {
                _logger.LogTrace(0, _resourceManager.GetString("UndefinedContentLengthHeader"), context.Request.Headers["Accept"]);

                context.Response.StatusCode = (int)HttpStatusCode.LengthRequired;
            }
            else
            {
                var requestString = default(string);

                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    requestString = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                if (requestString.Length != context.Request.ContentLength.Value)
                {
                    _logger.LogTrace(0, _resourceManager.GetString("InvalidContentLengthHeader"), context.Request.Headers["Accept"]);

                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
                else
                {
                    _logger.LogInformation(0, _resourceManager.GetString("RequestTraceLine"), context.Request.PathBase, context.Connection.RemoteIpAddress);

                    var jsonRpcRequestData = default(JsonRpcData<JsonRpcRequest>);
                    var responseString = string.Empty;

                    try
                    {
                        jsonRpcRequestData = _serializer.DeserializeRequestsData(requestString);
                    }
                    catch (JsonRpcException ex)
                    {
                        _logger.LogTrace(1, ex, _resourceManager.GetString("RequestDeserializingError"));

                        responseString = _serializer.SerializeResponse(new JsonRpcResponse(ConvertToError(ex), JsonRpcId.None));
                    }

                    if (jsonRpcRequestData != null)
                    {
                        if (!jsonRpcRequestData.IsBatch)
                        {
                            var jsonRpcResponse = await ProcessRequest(jsonRpcRequestData.GetSingleItem()).ConfigureAwait(false);

                            if (jsonRpcResponse != default(JsonRpcResponse))
                            {
                                responseString = _serializer.SerializeResponse(jsonRpcResponse);
                            }
                            else
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                            }
                        }
                        else
                        {
                            var items = jsonRpcRequestData.GetBatchItems();
                            var jsonRpcResponses = new List<JsonRpcResponse>(items.Count);

                            for (var i = 0; i < items.Count; i++)
                            {
                                var jsonRpcResponse = await ProcessRequest(items[i]).ConfigureAwait(false);

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

        private static ResourceManager CreateResourceManager()
        {
            var assembly = typeof(JsonRpcMiddleware).GetTypeInfo().Assembly;

            return new ResourceManager($"{assembly.GetName().Name}.Resources.Strings", assembly);
        }
    }
}