using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Globalization;
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
using Microsoft.Net.Http.Headers;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcMiddleware<T> : IMiddleware, IDisposable
        where T : class, IJsonRpcHandler
    {
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
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Strings.GetString("handler.scheme.method.system_name"), kvp.Key));
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
            if (context.Response.HasStarted)
            {
                await FinishInvocationAsync(context, next).ConfigureAwait(false);

                return;
            }
            if (string.Compare(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase) != 0)
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.MethodNotAllowed).ConfigureAwait(false);

                return;
            }
            if (context.Request.QueryString.HasValue)
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.BadRequest).ConfigureAwait(false);

                return;
            }
            if (string.Compare(context.Request.ContentType, JsonRpcTransportConstants.MimeType, StringComparison.OrdinalIgnoreCase) != 0)
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.UnsupportedMediaType).ConfigureAwait(false);

                return;
            }
            if (context.Request.Headers[HeaderNames.ContentEncoding] != default(StringValues))
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.BadRequest).ConfigureAwait(false);

                return;
            }
            if (context.Request.ContentLength == null)
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.LengthRequired).ConfigureAwait(false);

                return;
            }
            if (string.Compare(context.Request.Headers[HeaderNames.Accept], JsonRpcTransportConstants.MimeType, StringComparison.OrdinalIgnoreCase) != 0)
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.NotAcceptable).ConfigureAwait(false);

                return;
            }

            var requestString = default(string);

            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                requestString = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (requestString.Length != context.Request.ContentLength.Value)
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.BadRequest).ConfigureAwait(false);

                return;
            }

            var responseString = default(string);

            try
            {
                responseString = await HandleJsonRpcContentAsync(context, requestString).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (context.RequestAborted.IsCancellationRequested)
            {
                await FinishInvocationAsync(context, next).ConfigureAwait(false);

                return;
            }

            if (responseString != null)
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.OK, Encoding.UTF8.GetBytes(responseString)).ConfigureAwait(false);
            }
            else
            {
                await FinishInvocationAsync(context, next, HttpStatusCode.NoContent).ConfigureAwait(false);
            }
        }

        private static async Task FinishInvocationAsync(HttpContext context, RequestDelegate next, HttpStatusCode? statusCode = null, byte[] body = null)
        {
            if (statusCode.HasValue)
            {
                context.Response.StatusCode = (int)statusCode.Value;

                if (body != null)
                {
                    context.Response.ContentType = JsonRpcTransportConstants.MimeType;
                    context.Response.ContentLength = body.Length;

                    await context.Response.Body.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                }
                else
                {
                    context.Response.Body.Write(Array.Empty<byte>(), 0, 0);
                }
            }

            await next.Invoke(context).ConfigureAwait(false);
        }

        private async Task<string> HandleJsonRpcContentAsync(HttpContext context, string content)
        {
            var requestData = default(JsonRpcData<JsonRpcRequest>);

            try
            {
                requestData = _serializer.DeserializeRequestData(content);
            }
            catch (JsonRpcException ex)
            {
                _logger?.LogError(1000, ex, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                var jsonRpcError = ConvertExceptionToError(ex);

                context.Items[JsonRpcTransportConstants.ScopeErrorsIdentifier] = new[] { jsonRpcError.Code };

                return _serializer.SerializeResponse(new JsonRpcResponse(jsonRpcError));
            }

            if (requestData.IsSingle)
            {
                var requestItem = requestData.SingleItem;

                _logger?.LogDebug(4000, Strings.GetString("handler.request_data.accepted_single"), context.TraceIdentifier, context.Request.PathBase);

                context.RequestAborted.ThrowIfCancellationRequested();

                var response = await HandleJsonRpcItemAsync(context, requestItem).ConfigureAwait(false);

                if (response == null)
                {
                    return null;
                }
                if (!response.Success)
                {
                    context.Items[JsonRpcTransportConstants.ScopeErrorsIdentifier] = new[] { response.Error.Code };
                }
                if (requestItem.IsValid && requestItem.Message.IsNotification)
                {
                    return null;
                }

                context.RequestAborted.ThrowIfCancellationRequested();

                return _serializer.SerializeResponse(response);
            }
            else
            {
                var requestItems = requestData.BatchItems;

                _logger?.LogDebug(4010, Strings.GetString("handler.request_data.accepted_batch"), context.TraceIdentifier, requestItems.Count, context.Request.PathBase);

                var maximumBatchSize = _options?.MaxBatchSize ?? 1024;

                if (requestItems.Count > maximumBatchSize)
                {
                    _logger?.LogError(1040, Strings.GetString("handler.request_data.invalid_batch_size"), context.TraceIdentifier, requestItems.Count, maximumBatchSize);

                    var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.InvalidBatchSize, Strings.GetString("rpc.error.invalid_batch_size"));

                    context.Items[JsonRpcTransportConstants.ScopeErrorsIdentifier] = new[] { jsonRpcError.Code };

                    return _serializer.SerializeResponse(new JsonRpcResponse(jsonRpcError));
                }

                var identifiers = new HashSet<JsonRpcId>();

                for (var i = 0; i < requestItems.Count; i++)
                {
                    var requestItem = requestItems[i];

                    if (requestItem.IsValid && !requestItem.Message.IsNotification)
                    {
                        if (!identifiers.Add(requestItem.Message.Id))
                        {
                            _logger?.LogError(1020, Strings.GetString("handler.request_data.duplicate_ids"), context.TraceIdentifier);

                            var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.DuplicateIdentifiers, Strings.GetString("rpc.error.duplicate_ids"));

                            context.Items[JsonRpcTransportConstants.ScopeErrorsIdentifier] = new[] { jsonRpcError.Code };

                            return _serializer.SerializeResponse(new JsonRpcResponse(jsonRpcError));
                        }
                    }
                }

                var responses = new List<JsonRpcResponse>();
                var errorCodes = new List<long>();

                for (var i = 0; i < requestItems.Count; i++)
                {
                    context.RequestAborted.ThrowIfCancellationRequested();

                    var requestItem = requestItems[i];
                    var response = await HandleJsonRpcItemAsync(context, requestItem).ConfigureAwait(false);

                    if (response != null)
                    {
                        if (!response.Success)
                        {
                            errorCodes.Add(response.Error.Code);
                        }
                        if (requestItem.IsValid && requestItem.Message.IsNotification)
                        {
                            continue;
                        }

                        responses.Add(response);
                    }
                }

                if (errorCodes.Count > 0)
                {
                    context.Items[JsonRpcTransportConstants.ScopeErrorsIdentifier] = errorCodes;
                }
                if (responses.Count == 0)
                {
                    // Server must return empty content for empty response batch according to the JSON-RPC 2.0 specification

                    return null;
                }

                context.RequestAborted.ThrowIfCancellationRequested();

                return _serializer.SerializeResponses(responses);
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
                var maximumIdLength = _options?.MaxIdLength ?? 128;
                var currentIdLength = ((string)request.Id).Length;

                if (currentIdLength > maximumIdLength)
                {
                    _logger?.LogError(1030, Strings.GetString("handler.request.invalid_id_length"), context.TraceIdentifier, currentIdLength, maximumIdLength);

                    return new JsonRpcResponse(new JsonRpcError(JsonRpcTransportErrorCodes.InvalidIdLength, Strings.GetString("rpc.error.invalid_id_length")));
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
                }
                else
                {
                    if (response.Success)
                    {
                        _logger?.LogInformation(3030, Strings.GetString("handler.response.handled_with_result_as_notification"), context.TraceIdentifier, request.Method);
                    }
                    else
                    {
                        _logger?.LogInformation(3040, Strings.GetString("handler.response.handled_with_error_as_notification"), context.TraceIdentifier, request.Method, response.Error.Code, response.Error.Message);
                    }
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