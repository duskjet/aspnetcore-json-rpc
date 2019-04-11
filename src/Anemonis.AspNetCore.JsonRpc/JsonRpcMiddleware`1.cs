﻿// © Alexander Kozlenko. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Anemonis.AspNetCore.JsonRpc.Resources;
using Anemonis.JsonRpc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace Anemonis.AspNetCore.JsonRpc
{
    /// <summary>Represents a middleware for adding a JSON-RPC handler to the application's request pipeline.</summary>
    /// <typeparam name="T">The type of the handler.</typeparam>
    public sealed class JsonRpcMiddleware<T> : IMiddleware, IDisposable
        where T : class, IJsonRpcHandler
    {
        private const string _mediaTypeValue = "application/json";
        private const string _contentTypeHeaderValue = "application/json; charset=utf-8";
        private const int _streamBufferSize = 1024;

        private static readonly IReadOnlyDictionary<string, Encoding> _supportedEncodings = CreateSupportedEncodings();
        private static readonly IReadOnlyDictionary<long, JsonRpcError> _standardJsonRpcErrors = CreateStandardJsonRpcErrors();

        private readonly T _handler;
        private readonly JsonRpcSerializer _serializer;
        private readonly ILogger _logger;

        /// <summary>Initializes a new instance of the <see cref="JsonRpcMiddleware{T}" /> class.</summary>
        /// <param name="services">The <see cref="IServiceProvider" /> instance for retrieving service objects.</param>
        /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
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

        private static IReadOnlyDictionary<string, Encoding> CreateSupportedEncodings()
        {
            return new Dictionary<string, Encoding>(StringComparer.OrdinalIgnoreCase)
            {
                [Encoding.UTF8.WebName] = new UTF8Encoding(false, true),
                [Encoding.Unicode.WebName] = new UnicodeEncoding(false, false, true),
                [Encoding.UTF32.WebName] = new UTF32Encoding(false, false, true)
            };
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
                if (JsonRpcProtocol.IsSystemMethod(kvp.Key))
                {
                    throw new InvalidOperationException(string.Format(Strings.GetString("handler.contract.method.system_name"), kvp.Key));
                }

                resolver.AddRequestContract(kvp.Key, kvp.Value);
            }

            return resolver;
        }

        private static IReadOnlyDictionary<long, JsonRpcError> CreateStandardJsonRpcErrors()
        {
            return new Dictionary<long, JsonRpcError>(5)
            {
                [JsonRpcErrorCode.InvalidFormat] = new JsonRpcError(JsonRpcErrorCode.InvalidFormat, Strings.GetString("rpc.error.invalid_format")),
                [JsonRpcErrorCode.InvalidOperation] = new JsonRpcError(JsonRpcErrorCode.InvalidOperation, Strings.GetString("rpc.error.invalid_operation")),
                [JsonRpcErrorCode.InvalidParameters] = new JsonRpcError(JsonRpcErrorCode.InvalidParameters, Strings.GetString("rpc.error.invalid_parameters")),
                [JsonRpcErrorCode.InvalidMethod] = new JsonRpcError(JsonRpcErrorCode.InvalidMethod, Strings.GetString("rpc.error.invalid_method")),
                [JsonRpcErrorCode.InvalidMessage] = new JsonRpcError(JsonRpcErrorCode.InvalidMessage, Strings.GetString("rpc.error.invalid_message"))
            };
        }

        private static JsonRpcError ConvertExceptionToJsonRpcError(JsonRpcSerializationException exception)
        {
            if (!_standardJsonRpcErrors.TryGetValue(exception.ErrorCode, out var jsonRpcError))
            {
                jsonRpcError = new JsonRpcError(exception.ErrorCode, Strings.GetString("rpc.error.invalid_operation"));
            }

            return jsonRpcError;
        }

        /// <summary>Handles an HTTP request as an asynchronous operation.</summary>
        /// <param name="context">The <see cref="HttpContext" /> instance for the current request.</param>
        /// <param name="next">The delegate representing the remaining middleware in the request pipeline.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
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
            if (context.Request.Headers.ContainsKey(HeaderNames.ContentEncoding))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }

            var requestStreamEncoding = default(Encoding);
            var responseStreamEncoding = default(Encoding);

            if (!context.Request.Headers.TryGetValue(HeaderNames.ContentType, out var contentTypeHeaderValueString))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (!MediaTypeHeaderValue.TryParse((string)contentTypeHeaderValueString, out var contentTypeHeaderValue))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (!contentTypeHeaderValue.MediaType.Equals(_mediaTypeValue, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (contentTypeHeaderValue.Charset.HasValue && !_supportedEncodings.TryGetValue(contentTypeHeaderValue.Charset.Value, out requestStreamEncoding))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;

                return;
            }
            if (!context.Request.Headers.TryGetValue(HeaderNames.Accept, out var acceptTypeHeaderValueString))
            {
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;

                return;
            }
            if (!MediaTypeHeaderValue.TryParse((string)acceptTypeHeaderValueString, out var acceptTypeHeaderValue))
            {
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;

                return;
            }
            if (!acceptTypeHeaderValue.MediaType.Equals(_mediaTypeValue, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;

                return;
            }
            if (acceptTypeHeaderValue.Charset.HasValue && !_supportedEncodings.TryGetValue(acceptTypeHeaderValue.Charset.Value, out responseStreamEncoding))
            {
                context.Response.StatusCode = StatusCodes.Status406NotAcceptable;

                return;
            }

            if (requestStreamEncoding == null)
            {
                requestStreamEncoding = _supportedEncodings[Encoding.UTF8.WebName];
            }
            if (responseStreamEncoding == null)
            {
                responseStreamEncoding = _supportedEncodings[Encoding.UTF8.WebName];
            }

            var jsonRpcRequestData = default(JsonRpcData<JsonRpcRequest>);

            try
            {
                using (var streamReader = new StreamReader(context.Request.Body, requestStreamEncoding, false, _streamBufferSize, true))
                {
                    jsonRpcRequestData = await _serializer.DeserializeRequestDataAsync(streamReader, context.RequestAborted);
                }
            }
            catch (JsonException e)
            {
                _logger?.LogError(4000, e, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                var jsonRpcError = new JsonRpcError(JsonRpcErrorCode.InvalidFormat, Strings.GetString("rpc.error.invalid_format"));
                var jsonRpcResponse = new JsonRpcResponse(default, jsonRpcError);

                context.Response.StatusCode = StatusCodes.Status200OK;

                await SerializeJsonRpcResponseAsync(context, jsonRpcResponse, responseStreamEncoding);

                return;
            }
            catch (JsonRpcSerializationException e)
            {
                _logger?.LogError(4000, e, Strings.GetString("handler.request_data.declined"), context.TraceIdentifier, context.Request.PathBase);

                var jsonRpcError = ConvertExceptionToJsonRpcError(e);
                var jsonRpcResponse = new JsonRpcResponse(default, jsonRpcError);

                context.Response.StatusCode = StatusCodes.Status200OK;

                await SerializeJsonRpcResponseAsync(context, jsonRpcResponse, responseStreamEncoding);

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

                await SerializeJsonRpcResponseAsync(context, jsonRpcResponse, responseStreamEncoding);
            }
            else
            {
                var jsonRpcRequestItems = jsonRpcRequestData.Items;

                _logger?.LogDebug(1010, Strings.GetString("handler.request_data.accepted_batch"), context.TraceIdentifier, jsonRpcRequestItems.Count, context.Request.PathBase);

#if NETCOREAPP2_1

                var jsonRpcRequestIdentifiers = new HashSet<JsonRpcId>(jsonRpcRequestItems.Count);

#else

                var jsonRpcRequestIdentifiers = new HashSet<JsonRpcId>();

#endif

                for (var i = 0; i < jsonRpcRequestItems.Count; i++)
                {
                    var jsonRpcRequestItem = jsonRpcRequestItems[i];

                    if (jsonRpcRequestItem.IsValid && !jsonRpcRequestItem.Message.IsNotification)
                    {
                        if (!jsonRpcRequestIdentifiers.Add(jsonRpcRequestItem.Message.Id))
                        {
                            _logger?.LogError(4020, Strings.GetString("handler.request_data.duplicate_ids"), context.TraceIdentifier);

                            var jsonRpcError = new JsonRpcError(-32000L, Strings.GetString("rpc.error.duplicate_ids"));
                            var jsonRpcResponse = new JsonRpcResponse(default, jsonRpcError);

                            context.Response.StatusCode = StatusCodes.Status200OK;

                            await SerializeJsonRpcResponseAsync(context, jsonRpcResponse, responseStreamEncoding);

                            return;
                        }
                    }
                }

                var jsonRpcResponses = new List<JsonRpcResponse>(jsonRpcRequestItems.Count);

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

                await SerializeJsonRpcResponsesAsync(context, jsonRpcResponses, responseStreamEncoding);
            }
        }

        private async Task SerializeJsonRpcResponseAsync(HttpContext context, JsonRpcResponse jsonRpcResponse, Encoding encoding)
        {
            context.Response.ContentType = _contentTypeHeaderValue;

            using (var responseStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(responseStream, encoding, _streamBufferSize, true))
                {
                    await _serializer.SerializeResponseAsync(jsonRpcResponse, streamWriter, context.RequestAborted);
                }

                context.Response.ContentLength = responseStream.Length;
                responseStream.Position = 0;

                await responseStream.CopyToAsync(context.Response.Body);
            }
        }

        private async Task SerializeJsonRpcResponsesAsync(HttpContext context, IReadOnlyList<JsonRpcResponse> jsonRpcResponses, Encoding encoding)
        {
            context.Response.ContentType = _contentTypeHeaderValue;

            using (var responseStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(responseStream, encoding, _streamBufferSize, true))
                {
                    await _serializer.SerializeResponsesAsync(jsonRpcResponses, streamWriter, context.RequestAborted);
                }

                context.Response.ContentLength = responseStream.Length;
                responseStream.Position = 0;

                await responseStream.CopyToAsync(context.Response.Body);
            }
        }

        private async Task<JsonRpcResponse> CreateJsonRpcResponseAsync(HttpContext context, JsonRpcMessageInfo<JsonRpcRequest> requestInfo)
        {
            if (!requestInfo.IsValid)
            {
                var exception = requestInfo.Exception;

                _logger?.LogError(4010, exception, Strings.GetString("handler.request.invalid_message"), context.TraceIdentifier, exception.MessageId);

                return new JsonRpcResponse(exception.MessageId, ConvertExceptionToJsonRpcError(exception));
            }

            var request = requestInfo.Message;
            var requestId = request.Id;
            var response = await _handler.HandleAsync(request);

            if (response != null)
            {
                var responseId = response.Id;

                if (requestId != responseId)
                {
                    throw new InvalidOperationException(Strings.GetString("handler.response.invalid_id"));
                }

                if (!request.IsNotification)
                {
                    if (response.Success)
                    {
                        _logger?.LogInformation(2010, Strings.GetString("handler.response.handled_with_result"), context.TraceIdentifier, requestId, request.Method);
                    }
                    else
                    {
                        _logger?.LogInformation(2020, Strings.GetString("handler.response.handled_with_error"), context.TraceIdentifier, requestId, request.Method, response.Error.Code, response.Error.Message);
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
                    _logger?.LogWarning(3000, Strings.GetString("handler.response.handled_notification_as_response"), context.TraceIdentifier, requestId, request.Method);
                }
            }

            return response;
        }

        /// <summary>Disposes the corresponding instance of a JSON-RPC handler.</summary>
        public void Dispose()
        {
            (_handler as IDisposable)?.Dispose();
        }
    }
}