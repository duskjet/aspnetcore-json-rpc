// © Alexander Kozlenko. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Globalization;
using System.IO;
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

            _handler = services.GetService<T>() ?? ActivatorUtilities.CreateInstance<T>(services);

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
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;

                return;
            }
            if (!StringSegment.Equals(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (context.Request.Headers.ContainsKey(HeaderNames.ContentEncoding))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (!StringSegment.Equals((string)context.Request.Headers[HeaderNames.Accept], "application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;

                return;
            }

            var jsonRpcRequestData = default(JsonRpcData<JsonRpcRequest>);

            try
            {
                jsonRpcRequestData = await _serializer.DeserializeRequestDataAsync(context.Request.Body, context.RequestAborted);
            }
            catch (JsonRpcException e)
            {
                _logger?.LogError(4000, e, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                var jsonRpcError = ConvertExceptionToError(e);

                if (_diagnostic != null)
                {
                    await _diagnostic.HandleErrorAsync(jsonRpcError.Code);
                }

                var jsonRpcResponse = new JsonRpcResponse(jsonRpcError);

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
                if (!jsonRpcResponse.Success)
                {
                    if (_diagnostic != null)
                    {
                        await _diagnostic.HandleErrorAsync(jsonRpcResponse.Error.Code);
                    }
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

                var maximumBatchSize = _options?.MaxBatchSize ?? 1024;

                if (jsonRpcRequestItems.Count > maximumBatchSize)
                {
                    _logger?.LogError(4040, Strings.GetString("handler.request_data.invalid_batch_size"), context.TraceIdentifier, jsonRpcRequestItems.Count, maximumBatchSize);

                    var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.InvalidBatchSize, Strings.GetString("rpc.error.invalid_batch_size"));

                    if (_diagnostic != null)
                    {
                        await _diagnostic.HandleErrorAsync(jsonRpcError.Code);
                    }

                    var jsonRpcResponse = new JsonRpcResponse(jsonRpcError);

                    context.Response.StatusCode = StatusCodes.Status200OK;

                    await SerializeResponseAsync(context, jsonRpcResponse);

                    return;
                }

                var jsonRpcRequestIdentifiers = new HashSet<JsonRpcId>();

                for (var i = 0; i < jsonRpcRequestItems.Count; i++)
                {
                    var jsonRpcRequestItem = jsonRpcRequestItems[i];

                    if (jsonRpcRequestItem.IsValid && !jsonRpcRequestItem.Message.IsNotification)
                    {
                        if (!jsonRpcRequestIdentifiers.Add(jsonRpcRequestItem.Message.Id))
                        {
                            _logger?.LogError(4020, Strings.GetString("handler.request_data.duplicate_ids"), context.TraceIdentifier);

                            var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.DuplicateIdentifiers, Strings.GetString("rpc.error.duplicate_ids"));

                            if (_diagnostic != null)
                            {
                                await _diagnostic.HandleErrorAsync(jsonRpcError.Code);
                            }

                            var jsonRpcResponse = new JsonRpcResponse(jsonRpcError);

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
                        if (!jsonRpcResponse.Success)
                        {
                            if (_diagnostic != null)
                            {
                                await _diagnostic.HandleErrorAsync(jsonRpcResponse.Error.Code);
                            }
                        }
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

            if (request.Id.Type == JsonRpcIdType.String)
            {
                var maximumIdLength = _options?.MaxIdLength ?? 128;
                var currentIdLength = ((string)request.Id).Length;

                if (currentIdLength > maximumIdLength)
                {
                    _logger?.LogError(4030, Strings.GetString("handler.request.invalid_id_length"), context.TraceIdentifier, currentIdLength, maximumIdLength);

                    var jsonRpcError = new JsonRpcError(JsonRpcTransportErrorCodes.InvalidIdLength, Strings.GetString("rpc.error.invalid_id_length"));

                    if (_diagnostic != null)
                    {
                        await _diagnostic.HandleErrorAsync(jsonRpcError.Code);
                    }

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

            (_handler as IDisposable)?.Dispose();
        }
    }
}