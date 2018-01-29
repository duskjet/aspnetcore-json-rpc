using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Resources;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcMiddleware<T> : IMiddleware, IDisposable
        where T : IJsonRpcHandler
    {
        private readonly JsonRpcSerializer _serializer;
        private readonly T _handler;
        private readonly bool _productionEnvironment;
        private readonly ILogger _logger;

        public JsonRpcMiddleware(IServiceProvider serviceProvider, IHostingEnvironment hostingEnvironment, ILoggerFactory loggerFactory)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _handler = ActivatorUtilities.CreateInstance<T>(serviceProvider);
            _productionEnvironment = hostingEnvironment?.EnvironmentName != EnvironmentName.Development;
            _logger = loggerFactory?.CreateLogger<JsonRpcMiddleware<T>>();

            var scheme = _handler.CreateScheme();

            _serializer = new JsonRpcSerializer(
                new Dictionary<string, JsonRpcRequestContract>(scheme.Count, StringComparer.Ordinal),
                new Dictionary<string, JsonRpcResponseContract>(0),
                new Dictionary<JsonRpcId, string>(0),
                new Dictionary<JsonRpcId, JsonRpcResponseContract>(0));

            foreach (var kvp in scheme)
            {
                if (kvp.Key == null)
                {
                    throw new InvalidOperationException(Strings.GetString("handler.scheme.method.undefined_name"));
                }
                if (JsonRpcRequest.IsSystemMethod(kvp.Key))
                {
                    throw new InvalidOperationException(Strings.GetString("handler.scheme.method.system_name"));
                }

                _serializer.RequestContracts[kvp.Key] = kvp.Value;
            }
        }

        async Task IMiddleware.InvokeAsync(HttpContext context, RequestDelegate next)
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

                        if (context.RequestAborted.IsCancellationRequested)
                        {
                            return;
                        }
                    }
                    catch (JsonRpcException ex)
                    {
                        _logger?.LogError(1000, ex, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                        responseString = _serializer.SerializeResponse(new JsonRpcResponse(ConvertExceptionToError(ex), default));
                    }

                    if (jsonRpcRequestData != null)
                    {
                        if (jsonRpcRequestData.IsSingle)
                        {
                            _logger?.LogTrace(4010, Strings.GetString("handler.request_data.accepted_single"), context.TraceIdentifier, context.Request.PathBase);

                            var jsonRpcResponse = default(JsonRpcResponse);

                            try
                            {
                                jsonRpcResponse = await InvokeHandlerAsync(context, jsonRpcRequestData.SingleItem).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                                when (context.RequestAborted.IsCancellationRequested)
                            {
                                return;
                            }

                            if (context.RequestAborted.IsCancellationRequested)
                            {
                                return;
                            }

                            if (jsonRpcResponse != null)
                            {
                                responseString = _serializer.SerializeResponse(jsonRpcResponse);

                                if (context.RequestAborted.IsCancellationRequested)
                                {
                                    return;
                                }
                            }
                            else
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                            }
                        }
                        else
                        {
                            _logger?.LogTrace(4020, Strings.GetString("handler.request_data.accepted_batch"), context.TraceIdentifier, jsonRpcRequestData.BatchItems.Count, context.Request.PathBase);

                            var jsonRpcResponses = new List<JsonRpcResponse>();

                            for (var i = 0; i < jsonRpcRequestData.BatchItems.Count; i++)
                            {
                                var jsonRpcResponse = default(JsonRpcResponse);

                                try
                                {
                                    jsonRpcResponse = await InvokeHandlerAsync(context, jsonRpcRequestData.BatchItems[i]).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                    when (context.RequestAborted.IsCancellationRequested)
                                {
                                    return;
                                }

                                if (context.RequestAborted.IsCancellationRequested)
                                {
                                    return;
                                }
                                if (jsonRpcResponse != null)
                                {
                                    jsonRpcResponses.Add(jsonRpcResponse);
                                }
                            }

                            responseString = _serializer.SerializeResponses(jsonRpcResponses);

                            if (context.RequestAborted.IsCancellationRequested)
                            {
                                return;
                            }
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

        private async Task<JsonRpcResponse> InvokeHandlerAsync(HttpContext context, JsonRpcItem<JsonRpcRequest> item)
        {
            var response = default(JsonRpcResponse);

            if (item.IsValid)
            {
                var request = item.Message;

                response = await _handler.HandleAsync(request).ConfigureAwait(false);

                if (response != null)
                {
                    if (request.Id != response.Id)
                    {
                        throw new InvalidOperationException(Strings.GetString("handler.response.id.invalid_value"));
                    }

                    if (!request.IsNotification)
                    {
                        if (response.Success)
                        {
                            _logger?.LogInformation(3000, Strings.GetString("handler.response.handled_with_result"), context.TraceIdentifier, request.Id, request.Method);
                        }
                        else
                        {
                            _logger?.LogInformation(3010, Strings.GetString("handler.response.handled_with_error"), context.TraceIdentifier, request.Id, request.Method, response.Error.Code);
                        }
                    }
                    else
                    {
                        response = null;

                        _logger?.LogWarning(2010, Strings.GetString("handler.response.handled_response_as_notification"), context.TraceIdentifier, request.Method);
                    }
                }
                else
                {
                    if (request.IsNotification)
                    {
                        _logger?.LogInformation(3020, Strings.GetString("handler.response.handled_notification"), context.TraceIdentifier, request.Method);
                    }
                    else
                    {
                        _logger?.LogWarning(2000, Strings.GetString("handler.response.handled_notification_as_response"), context.TraceIdentifier, request.Id, request.Method);
                    }
                }
            }
            else
            {
                var exception = item.Exception;

                response = new JsonRpcResponse(ConvertExceptionToError(exception), exception.MessageId);

                _logger?.LogError(1100, exception, Strings.GetString("handler.request.invalid_message"), context.TraceIdentifier, exception.MessageId);
            }

            return response;
        }

        private JsonRpcError ConvertExceptionToError(JsonRpcException exception)
        {
            var code = default(long);
            var message = default(string);

            switch (exception.Type)
            {
                case JsonRpcExceptionType.Parsing:
                    {
                        code = (long)JsonRpcErrorType.Parsing;
                        message = !_productionEnvironment ? exception.Message : Strings.GetString("rpc.error.parsing");
                    }
                    break;
                case JsonRpcExceptionType.InvalidParams:
                    {
                        code = (long)JsonRpcErrorType.InvalidParams;
                        message = !_productionEnvironment ? exception.Message : Strings.GetString("rpc.error.invalid_params");
                    }
                    break;
                case JsonRpcExceptionType.InvalidMethod:
                    {
                        code = (long)JsonRpcErrorType.InvalidMethod;
                        message = !_productionEnvironment ? exception.Message : Strings.GetString("rpc.error.invalid_method");
                    }
                    break;
                case JsonRpcExceptionType.InvalidMessage:
                    {
                        code = (long)JsonRpcErrorType.InvalidRequest;
                        message = !_productionEnvironment ? exception.Message : Strings.GetString("rpc.error.invalid_request");
                    }
                    break;
                default:
                    {
                        code = (long)JsonRpcErrorType.Internal;
                        message = !_productionEnvironment ? exception.Message : Strings.GetString("rpc.error.internal");
                    }
                    break;
            }

            return new JsonRpcError(code, message);
        }

        void IDisposable.Dispose()
        {
            _serializer.Dispose();
            (_handler as IDisposable)?.Dispose();
        }
    }
}