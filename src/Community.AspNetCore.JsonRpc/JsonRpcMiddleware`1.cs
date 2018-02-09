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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcMiddleware<T> : IMiddleware, IDisposable
        where T : class, IJsonRpcHandler
    {
        private const string _MEDIA_TYPE = "application/json";

        private readonly T _handler;
        private readonly bool _dispose;
        private readonly JsonRpcSerializer _serializer;
        private readonly bool _production;
        private readonly JsonRpcOptions _options;
        private readonly ILogger _logger;

        public JsonRpcMiddleware(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _handler = serviceProvider.GetService<T>();

            if (_handler == null)
            {
                _handler = ActivatorUtilities.CreateInstance<T>(serviceProvider);
                _dispose = _handler is IDisposable;
            }

            var scheme = _handler.CreateScheme();
            var contracts = new Dictionary<string, JsonRpcRequestContract>(scheme.Count, StringComparer.Ordinal);

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

                contracts[kvp.Key] = kvp.Value;
            }

            _serializer = new JsonRpcSerializer(
                contracts,
                new Dictionary<string, JsonRpcResponseContract>(0),
                new Dictionary<JsonRpcId, string>(0),
                new Dictionary<JsonRpcId, JsonRpcResponseContract>(0));

            _production = serviceProvider.GetService<IHostingEnvironment>()?.EnvironmentName != EnvironmentName.Development;
            _options = serviceProvider.GetService<IOptions<JsonRpcOptions>>()?.Value;
            _logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<JsonRpcMiddleware<T>>();
        }

        async Task IMiddleware.InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (string.Compare(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase) != 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;

                return;
            }
            if (context.Request.QueryString.HasValue)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

                return;
            }
            if (string.Compare(context.Request.ContentType, _MEDIA_TYPE, StringComparison.OrdinalIgnoreCase) != 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;

                return;
            }
            if (context.Request.Headers["Content-Encoding"] != default(StringValues))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

                return;
            }
            if (context.Request.ContentLength == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.LengthRequired;

                return;
            }
            if (string.Compare(context.Request.Headers["Accept"], _MEDIA_TYPE, StringComparison.OrdinalIgnoreCase) != 0)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;

                return;
            }

            var requestString = default(string);

            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                requestString = reader.ReadToEnd();
            }

            if (requestString.Length != context.Request.ContentLength.Value)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

                return;
            }

            var responseString = default(string);

            try
            {
                responseString = await HandleJsonRpcStringAsync(context, requestString).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (context.RequestAborted.IsCancellationRequested)
            {
                return;
            }

            if (responseString != null)
            {
                var responseBytes = Encoding.UTF8.GetBytes(responseString);

                context.Response.ContentType = _MEDIA_TYPE;
                context.Response.ContentLength = responseBytes.Length;
                context.Response.Body.Write(responseBytes, 0, responseBytes.Length);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            }
        }

        private async Task<string> HandleJsonRpcStringAsync(HttpContext context, string content)
        {
            var requestData = default(JsonRpcData<JsonRpcRequest>);

            try
            {
                requestData = _serializer.DeserializeRequestData(content);
            }
            catch (JsonRpcException ex)
            {
                _logger?.LogError(1000, ex, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                return _serializer.SerializeResponse(new JsonRpcResponse(ConvertExceptionToError(ex)));
            }

            if (requestData.IsSingle)
            {
                _logger?.LogTrace(4000, Strings.GetString("handler.request_data.accepted_single"), context.TraceIdentifier, context.Request.PathBase);

                context.RequestAborted.ThrowIfCancellationRequested();

                var response = await HandleJsonRpcItemAsync(context, requestData.SingleItem).ConfigureAwait(false);

                if (response != null)
                {
                    context.RequestAborted.ThrowIfCancellationRequested();

                    return _serializer.SerializeResponse(response);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                _logger?.LogTrace(4010, Strings.GetString("handler.request_data.accepted_batch"), context.TraceIdentifier, requestData.BatchItems.Count, context.Request.PathBase);

                var maximumBatchSize = _options?.MaxBatchSize ?? 1024;

                if (requestData.BatchItems.Count > maximumBatchSize)
                {
                    _logger?.LogError(1040, Strings.GetString("handler.request_data.invalid_batch_size"), context.TraceIdentifier, requestData.BatchItems.Count, maximumBatchSize);

                    return _serializer.SerializeResponse(new JsonRpcResponse(new JsonRpcError(-32020, Strings.GetString("rpc.error.invalid_batch_size"))));
                }

                var identifiers = new HashSet<JsonRpcId>();

                for (var i = 0; i < requestData.BatchItems.Count; i++)
                {
                    var requestItem = requestData.BatchItems[i];

                    if (requestItem.IsValid && !requestItem.Message.IsNotification)
                    {
                        if (!identifiers.Add(requestItem.Message.Id))
                        {
                            _logger?.LogError(1020, Strings.GetString("handler.request_data.duplicate_ids"), context.TraceIdentifier);

                            return _serializer.SerializeResponse(new JsonRpcResponse(new JsonRpcError(-32000, Strings.GetString("rpc.error.duplicate_ids"))));
                        }
                    }
                }

                var responses = new List<JsonRpcResponse>();

                for (var i = 0; i < requestData.BatchItems.Count; i++)
                {
                    context.RequestAborted.ThrowIfCancellationRequested();

                    var response = await HandleJsonRpcItemAsync(context, requestData.BatchItems[i]).ConfigureAwait(false);

                    if (response != null)
                    {
                        responses.Add(response);
                    }
                }

                if (responses.Count != 0)
                {
                    context.RequestAborted.ThrowIfCancellationRequested();

                    return _serializer.SerializeResponses(responses);
                }
                else
                {
                    // Server must return empty content for empty response batch according to the JSON-RPC 2.0 specification

                    return null;
                }
            }
        }

        private async Task<JsonRpcResponse> HandleJsonRpcItemAsync(HttpContext context, JsonRpcItem<JsonRpcRequest> requestItem)
        {
            if (!requestItem.IsValid)
            {
                var exception = requestItem.Exception;

                _logger?.LogError(1010, exception, Strings.GetString("handler.request.invalid_message"), context.TraceIdentifier, exception.MessageId);

                return new JsonRpcResponse(ConvertExceptionToError(exception), exception.MessageId);
            }

            var request = requestItem.Message;

            if (request.Id.Type == JsonRpcIdType.String)
            {
                var maximumIdLength = _options?.MaxIdLength ?? 1024;
                var currentIdLength = ((string)request.Id).Length;

                if (currentIdLength > maximumIdLength)
                {
                    _logger?.LogError(1030, Strings.GetString("handler.request.invalid_id_length"), context.TraceIdentifier, currentIdLength, maximumIdLength);

                    return new JsonRpcResponse(new JsonRpcError(-32010, Strings.GetString("rpc.error.invalid_id_length")));
                }
            }

            var response = await _handler.HandleAsync(request).ConfigureAwait(false);

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
                        _logger?.LogInformation(3010, Strings.GetString("handler.response.handled_with_result"), context.TraceIdentifier, request.Id, request.Method);
                    }
                    else
                    {
                        _logger?.LogInformation(3020, Strings.GetString("handler.response.handled_with_error"), context.TraceIdentifier, request.Id, request.Method, response.Error.Code, response.Error.Message);
                    }

                    return response;
                }
                else
                {
                    if (response.Success)
                    {
                        _logger?.LogWarning(2010, Strings.GetString("handler.response.handled_with_result_as_notification"), context.TraceIdentifier, request.Id, request.Method);
                    }
                    else
                    {
                        _logger?.LogWarning(2020, Strings.GetString("handler.response.handled_with_error_as_notification"), context.TraceIdentifier, request.Id, request.Method, response.Error.Code, response.Error.Message);
                    }

                    return null;
                }
            }
            else
            {
                if (request.IsNotification)
                {
                    _logger?.LogInformation(3000, Strings.GetString("handler.response.handled_notification"), context.TraceIdentifier, request.Method);
                }
                else
                {
                    _logger?.LogWarning(2000, Strings.GetString("handler.response.handled_notification_as_response"), context.TraceIdentifier, request.Id, request.Method);
                }

                return null;
            }
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
                        message = !_production ? exception.Message : Strings.GetString("rpc.error.parsing");
                    }
                    break;
                case JsonRpcExceptionType.InvalidParams:
                    {
                        code = (long)JsonRpcErrorType.InvalidParams;
                        message = !_production ? exception.Message : Strings.GetString("rpc.error.invalid_params");
                    }
                    break;
                case JsonRpcExceptionType.InvalidMethod:
                    {
                        code = (long)JsonRpcErrorType.InvalidMethod;
                        message = !_production ? exception.Message : Strings.GetString("rpc.error.invalid_method");
                    }
                    break;
                case JsonRpcExceptionType.InvalidMessage:
                    {
                        code = (long)JsonRpcErrorType.InvalidRequest;
                        message = !_production ? exception.Message : Strings.GetString("rpc.error.invalid_request");
                    }
                    break;
                default:
                    {
                        code = (long)JsonRpcErrorType.Internal;
                        message = !_production ? exception.Message : Strings.GetString("rpc.error.internal");
                    }
                    break;
            }

            return new JsonRpcError(code, message);
        }

        void IDisposable.Dispose()
        {
            _serializer.Dispose();

            if (_dispose)
            {
                ((IDisposable)_handler).Dispose();
            }
        }
    }
}