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
        private readonly IJsonRpcDiagnosticProvider _diagnosticProvider;

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
            _diagnosticProvider = serviceProvider.GetService<IJsonRpcDiagnosticProvider>();
        }

        async Task IMiddleware.InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Response.HasStarted)
            {
                return;
            }
            if (string.Compare(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase) != 0)
            {
                await CreateResponseAsync(context, HttpStatusCode.MethodNotAllowed);

                return;
            }
            if (context.Request.QueryString.HasValue)
            {
                await CreateResponseAsync(context, HttpStatusCode.BadRequest);

                return;
            }
            if (string.Compare(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase) != 0)
            {
                await CreateResponseAsync(context, HttpStatusCode.UnsupportedMediaType);

                return;
            }

            var encodingName = context.Request.Headers[HeaderNames.ContentEncoding];

            if (encodingName != default(StringValues))
            {
                if (string.Compare(encodingName, "identity", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    await CreateResponseAsync(context, HttpStatusCode.UnsupportedMediaType);

                    return;
                }
            }
            if (context.Request.ContentLength == null)
            {
                await CreateResponseAsync(context, HttpStatusCode.LengthRequired);

                return;
            }
            if (string.Compare(context.Request.Headers[HeaderNames.Accept], "application/json", StringComparison.OrdinalIgnoreCase) != 0)
            {
                await CreateResponseAsync(context, HttpStatusCode.NotAcceptable);

                return;
            }

            var requestString = default(string);

            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                requestString = await reader.ReadToEndAsync();
            }

            if (requestString.Length != context.Request.ContentLength.Value)
            {
                await CreateResponseAsync(context, HttpStatusCode.BadRequest);

                return;
            }

            var responseString = await HandleJsonRpcContentAsync(context, requestString);

            if (responseString != null)
            {
                await CreateResponseAsync(context, HttpStatusCode.OK, Encoding.UTF8.GetBytes(responseString));
            }
            else
            {
                await CreateResponseAsync(context, HttpStatusCode.NoContent);
            }
        }

        private static async Task CreateResponseAsync(HttpContext context, HttpStatusCode statusCode, byte[] body = null)
        {
            context.Response.StatusCode = (int)statusCode;

            if (body != null)
            {
                context.Response.ContentType = "application/json";
                context.Response.ContentLength = body.Length;

                await context.Response.Body.WriteAsync(body, 0, body.Length, context.RequestAborted);
            }
            else
            {
                context.Response.Body.Write(Array.Empty<byte>(), 0, 0);
            }
        }

        private async Task<string> HandleJsonRpcContentAsync(HttpContext context, string content)
        {
            var requestData = default(JsonRpcData<JsonRpcRequest>);

            try
            {
                requestData = _serializer.DeserializeRequestData(content);
            }
            catch (JsonRpcException e)
            {
                _logger?.LogError(4000, e, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                var jsonRpcError = ConvertExceptionToError(e);

                await HandleErrorAsync(jsonRpcError);

                return _serializer.SerializeResponse(new JsonRpcResponse(jsonRpcError));
            }

            if (requestData.IsSingle)
            {
                var requestItem = requestData.SingleItem;

                _logger?.LogDebug(1000, Strings.GetString("handler.request_data.accepted_single"), context.TraceIdentifier, context.Request.PathBase);

                context.RequestAborted.ThrowIfCancellationRequested();

                var response = await HandleJsonRpcItemAsync(context, requestItem);

                if (response == null)
                {
                    return null;
                }
                if (!response.Success)
                {
                    await HandleErrorAsync(response.Error);
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

                _logger?.LogDebug(1010, Strings.GetString("handler.request_data.accepted_batch"), context.TraceIdentifier, requestItems.Count, context.Request.PathBase);

                var maximumBatchSize = _options?.MaxBatchSize ?? 1024;

                if (requestItems.Count > maximumBatchSize)
                {
                    _logger?.LogError(4040, Strings.GetString("handler.request_data.invalid_batch_size"), context.TraceIdentifier, requestItems.Count, maximumBatchSize);

                    var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.InvalidBatchSize, Strings.GetString("rpc.error.invalid_batch_size"));

                    await HandleErrorAsync(jsonRpcError);

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
                            _logger?.LogError(4020, Strings.GetString("handler.request_data.duplicate_ids"), context.TraceIdentifier);

                            var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.DuplicateIdentifiers, Strings.GetString("rpc.error.duplicate_ids"));

                            await HandleErrorAsync(jsonRpcError);

                            return _serializer.SerializeResponse(new JsonRpcResponse(jsonRpcError));
                        }
                    }
                }

                var responses = new List<JsonRpcResponse>();

                for (var i = 0; i < requestItems.Count; i++)
                {
                    context.RequestAborted.ThrowIfCancellationRequested();

                    var requestItem = requestItems[i];
                    var response = await HandleJsonRpcItemAsync(context, requestItem);

                    if (response != null)
                    {
                        if (!response.Success)
                        {
                            await HandleErrorAsync(response.Error);
                        }
                        if (requestItem.IsValid && requestItem.Message.IsNotification)
                        {
                            continue;
                        }

                        responses.Add(response);
                    }
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

        private Task HandleErrorAsync(JsonRpcError error)
        {
            if (_diagnosticProvider == null)
            {
                return Task.CompletedTask;
            }

            return _diagnosticProvider.HandleErrorAsync(error.Code);
        }

        private async Task<JsonRpcResponse> HandleJsonRpcItemAsync(HttpContext context, JsonRpcItem<JsonRpcRequest> requestItem)
        {
            if (!requestItem.IsValid)
            {
                var exception = requestItem.Exception;

                _logger?.LogError(4010, exception, Strings.GetString("handler.request.invalid_message"), context.TraceIdentifier, exception.MessageId);

                return new JsonRpcResponse(ConvertExceptionToError(exception), exception.MessageId);
            }

            var request = requestItem.Message;

            if (request.Id.Type == JsonRpcIdType.String)
            {
                var maximumIdLength = _options?.MaxIdLength ?? 128;
                var currentIdLength = ((string)request.Id).Length;

                if (currentIdLength > maximumIdLength)
                {
                    _logger?.LogError(4030, Strings.GetString("handler.request.invalid_id_length"), context.TraceIdentifier, currentIdLength, maximumIdLength);

                    return new JsonRpcResponse(new JsonRpcError(JsonRpcTransportErrorCodes.InvalidIdLength, Strings.GetString("rpc.error.invalid_id_length")));
                }
            }

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