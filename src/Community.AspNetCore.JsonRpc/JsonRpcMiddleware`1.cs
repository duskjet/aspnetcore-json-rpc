using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Internal;
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
        private static IDictionary<string, JsonRpcRequestContract> _contracts;

        private readonly T _handler;
        private readonly bool _dispose;
        private readonly JsonRpcSerializer _serializer;
        private readonly bool _production;
        private readonly JsonRpcOptions _options;
        private readonly IJsonRpcDiagnosticProvider _diagnostic;
        private readonly ILogger _logger;

        public JsonRpcMiddleware(IServiceProvider services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            _handler = services.GetService<T>();

            if (_handler == null)
            {
                _handler = ActivatorUtilities.CreateInstance<T>(services);
                _dispose = _handler is IDisposable;
            }

            LazyInitializer.EnsureInitialized(ref _contracts, CreateContracts);

            _serializer = new JsonRpcSerializer(
                _contracts,
                EmptyDictionary<string, JsonRpcResponseContract>.Instance,
                EmptyDictionary<JsonRpcId, string>.Instance,
                EmptyDictionary<JsonRpcId, JsonRpcResponseContract>.Instance);

            _production = services.GetService<IHostingEnvironment>()?.EnvironmentName != EnvironmentName.Development;
            _options = services.GetService<IOptions<JsonRpcOptions>>()?.Value;
            _logger = services.GetService<ILoggerFactory>()?.CreateLogger<JsonRpcMiddleware<T>>();
            _diagnostic = services.GetService<IJsonRpcDiagnosticProvider>();
        }

        private IDictionary<string, JsonRpcRequestContract> CreateContracts()
        {
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

            return contracts;
        }

        async Task IMiddleware.InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Response.HasStarted)
            {
                return;
            }
            if (!StringSegment.Equals(context.Request.Method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase))
            {
                await CreateResponseAsync(context, StatusCodes.Status405MethodNotAllowed);

                return;
            }
            if (context.Request.QueryString.HasValue)
            {
                await CreateResponseAsync(context, StatusCodes.Status400BadRequest);

                return;
            }
            if (!StringSegment.Equals(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                await CreateResponseAsync(context, StatusCodes.Status415UnsupportedMediaType);

                return;
            }
            if (context.Request.Headers.ContainsKey(HeaderNames.ContentEncoding))
            {
                await CreateResponseAsync(context, StatusCodes.Status415UnsupportedMediaType);

                return;
            }
            if (context.Request.ContentLength == null)
            {
                await CreateResponseAsync(context, StatusCodes.Status411LengthRequired);

                return;
            }
            if (!StringSegment.Equals((string)context.Request.Headers[HeaderNames.Accept], "application/json", StringComparison.OrdinalIgnoreCase))
            {
                await CreateResponseAsync(context, StatusCodes.Status406NotAcceptable);

                return;
            }

            var requestString = default(string);

            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                requestString = await reader.ReadToEndAsync();
            }

            if (requestString.Length != context.Request.ContentLength.Value)
            {
                await CreateResponseAsync(context, StatusCodes.Status400BadRequest);

                return;
            }

            var responseString = await HandleJsonRpcContentAsync(context, requestString);

            if (responseString != null)
            {
                await CreateResponseAsync(context, StatusCodes.Status200OK, Encoding.UTF8.GetBytes(responseString));
            }
            else
            {
                await CreateResponseAsync(context, StatusCodes.Status204NoContent);
            }
        }

        private static async Task CreateResponseAsync(HttpContext context, int statusCode, byte[] body = null)
        {
            context.Response.StatusCode = statusCode;

            if (body != null)
            {
                context.Response.ContentType = "application/json";
                context.Response.ContentLength = body.Length;

                await context.Response.Body.WriteAsync(body, 0, body.Length, context.RequestAborted);
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

                await RegisterJsonRpcErrorAsync(jsonRpcError);

                return _serializer.SerializeResponse(new JsonRpcResponse(jsonRpcError));
            }

            if (!requestData.IsBatch)
            {
                var requestItem = requestData.Item;

                _logger?.LogDebug(1000, Strings.GetString("handler.request_data.accepted_single"), context.TraceIdentifier, context.Request.PathBase);

                context.RequestAborted.ThrowIfCancellationRequested();

                var response = await HandleJsonRpcItemAsync(context, requestItem);

                if (response == null)
                {
                    return null;
                }
                if (!response.Success)
                {
                    await RegisterJsonRpcErrorAsync(response.Error);
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
                var requestItems = requestData.Items;

                _logger?.LogDebug(1010, Strings.GetString("handler.request_data.accepted_batch"), context.TraceIdentifier, requestItems.Count, context.Request.PathBase);

                var maximumBatchSize = _options?.MaxBatchSize ?? 1024;

                if (requestItems.Count > maximumBatchSize)
                {
                    _logger?.LogError(4040, Strings.GetString("handler.request_data.invalid_batch_size"), context.TraceIdentifier, requestItems.Count, maximumBatchSize);

                    var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.InvalidBatchSize, Strings.GetString("rpc.error.invalid_batch_size"));

                    await RegisterJsonRpcErrorAsync(jsonRpcError);

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

                            await RegisterJsonRpcErrorAsync(jsonRpcError);

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
                            await RegisterJsonRpcErrorAsync(response.Error);
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
                    return null; // Server must return empty content for empty response batch according to the JSON-RPC 2.0 specification
                }

                context.RequestAborted.ThrowIfCancellationRequested();

                return _serializer.SerializeResponses(responses);
            }
        }

        private Task RegisterJsonRpcErrorAsync(JsonRpcError error)
        {
            if (_diagnostic == null)
            {
                return Task.CompletedTask;
            }

            return _diagnostic.HandleErrorAsync(error.Code);
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

                    var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.InvalidIdLength, Strings.GetString("rpc.error.invalid_id_length"));

                    await RegisterJsonRpcErrorAsync(jsonRpcError);

                    return new JsonRpcResponse(jsonRpcError);
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
            var message = default(string);

            if (_production)
            {
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
            }
            else
            {
                message = exception.Message;
            }

            return new JsonRpcError(exception.ErrorCode, message);
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