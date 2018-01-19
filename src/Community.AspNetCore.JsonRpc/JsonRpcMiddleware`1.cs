using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcMiddleware<T>
        where T : IJsonRpcHandler
    {
        private readonly JsonRpcSerializer _serializer = new JsonRpcSerializer();
        private readonly T _handler;
        private readonly ILogger _logger;

        public JsonRpcMiddleware(RequestDelegate next, IServiceProvider serviceProvider, ILoggerFactory loggerFactory, object args)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _handler = ActivatorUtilities.CreateInstance<T>(serviceProvider, (object[])args);
            _logger = loggerFactory?.CreateLogger<JsonRpcMiddleware<T>>();

            foreach (var kvp in _handler.CreateScheme())
            {
                _serializer.RequestContracts[kvp.Key] = kvp.Value;
            }
        }

        public async Task Invoke(HttpContext context)
        {
            if (string.Compare(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase) != 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else if (string.Compare(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase) != 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
            }
            else if (string.Compare(context.Request.Headers["Accept"], "application/json", StringComparison.OrdinalIgnoreCase) != 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
            }
            else if (context.Request.ContentLength == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.LengthRequired;
            }
            else
            {
                var requestString = default(string);

                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    requestString = reader.ReadToEnd();
                }

                if (requestString.Length != context.Request.ContentLength.Value)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
                else
                {
                    var jsonRpcRequestData = default(JsonRpcData<JsonRpcRequest>);
                    var responseString = default(string);

                    try
                    {
                        jsonRpcRequestData = _serializer.DeserializeRequestData(requestString);
                    }
                    catch (JsonRpcException ex)
                    {
                        responseString = _serializer.SerializeResponse(new JsonRpcResponse(ConvertExceptionToError(ex), default));

                        _logger?.LogInformation("JSON-RPC \"{0}\" [1] -> [1]", context.Request.PathBase);
                    }

                    if (jsonRpcRequestData != null)
                    {
                        if (jsonRpcRequestData.IsSingle)
                        {
                            var jsonRpcResponse = await InvokeHandlerAsync(jsonRpcRequestData.SingleItem).ConfigureAwait(false);

                            if (jsonRpcResponse != null)
                            {
                                responseString = _serializer.SerializeResponse(jsonRpcResponse);

                                _logger?.LogInformation("JSON-RPC \"{0}\" [1] -> [1]", context.Request.PathBase);
                            }
                            else
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.NoContent;

                                _logger?.LogInformation("JSON-RPC \"{0}\" [1] -> [0]", context.Request.PathBase);
                            }
                        }
                        else
                        {
                            var jsonRpcResponses = new List<JsonRpcResponse>(jsonRpcRequestData.BatchItems.Count);

                            for (var i = 0; i < jsonRpcRequestData.BatchItems.Count; i++)
                            {
                                var jsonRpcResponse = await InvokeHandlerAsync(jsonRpcRequestData.BatchItems[i]).ConfigureAwait(false);

                                if (jsonRpcResponse != null)
                                {
                                    jsonRpcResponses.Add(jsonRpcResponse);
                                }
                            }

                            responseString = _serializer.SerializeResponses(jsonRpcResponses);

                            _logger?.LogInformation("JSON-RPC \"{0}\" [{1}] -> [{2}]", context.Request.PathBase, jsonRpcRequestData.BatchItems.Count, jsonRpcResponses.Count);
                        }
                    }

                    if (context.Response.StatusCode != (int)HttpStatusCode.NoContent)
                    {
                        var responseBytes = Encoding.UTF8.GetBytes(responseString);

                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength = responseBytes.Length;
                        context.Response.Body.Write(responseBytes, 0, responseBytes.Length);
                    }
                }
            }
        }

        private async Task<JsonRpcResponse> InvokeHandlerAsync(JsonRpcItem<JsonRpcRequest> item)
        {
            if (item.IsValid)
            {
                var request = item.Message;

                if (request.IsSystem)
                {
                    _logger?.LogInformation(Strings.GetString("handler.request.system_message"), request.Method);

                    return null;
                }

                var response = await _handler.Handle(request).ConfigureAwait(false);

                if (response != null)
                {
                    if (request.Id != response.Id)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Strings.GetString("handler.response.id.invalid_value"), request.Id));
                    }

                    return !request.IsNotification ? response : null;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return new JsonRpcResponse(ConvertExceptionToError(item.Exception), item.Exception.MessageId);
            }
        }

        private static JsonRpcError ConvertExceptionToError(JsonRpcException exception)
        {
            var code = default(long);

            switch (exception.Type)
            {
                case JsonRpcExceptionType.Parsing:
                    {
                        code = (long)JsonRpcErrorType.Parsing;
                    }
                    break;
                case JsonRpcExceptionType.InvalidParams:
                    {
                        code = (long)JsonRpcErrorType.InvalidParams;
                    }
                    break;
                case JsonRpcExceptionType.InvalidMethod:
                    {
                        code = (long)JsonRpcErrorType.InvalidMethod;
                    }
                    break;
                case JsonRpcExceptionType.InvalidMessage:
                    {
                        code = (long)JsonRpcErrorType.InvalidRequest;
                    }
                    break;
                default:
                    {
                        code = (long)JsonRpcErrorType.Internal;
                    }
                    break;
            }

            return new JsonRpcError(code, exception.Message);
        }
    }
}