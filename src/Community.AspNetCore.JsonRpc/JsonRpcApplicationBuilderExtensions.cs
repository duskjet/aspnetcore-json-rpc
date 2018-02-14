using System;
using Community.AspNetCore.JsonRpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore
{
    /// <summary>The JSON-RPC 2.0 middleware extensions for the <see cref="IApplicationBuilder" />.</summary>
    public static class JsonRpcApplicationBuilderExtensions
    {
        /// <summary>Adds the specified JSON-RPC 2.0 handler for the specified path to the <see cref="IApplicationBuilder" /> instance.</summary>
        /// <param name="builder">The <see cref="IApplicationBuilder" /> to add the middleware to.</param>
        /// <param name="path">The request path for JSON-RPC methods.</param>
        /// <typeparam name="T">The type of the handler.</typeparam>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
        public static IApplicationBuilder UseJsonRpcHandler<T>(this IApplicationBuilder builder, PathString path = default)
            where T : class, IJsonRpcHandler
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.Map(path, b => b.UseMiddleware<JsonRpcMiddleware<T>>());
        }

        /// <summary>Adds the specified JSON-RPC 2.0 service for the specified path to the <see cref="IApplicationBuilder" /> instance.</summary>
        /// <param name="builder">The <see cref="IApplicationBuilder" /> to add the middleware to.</param>
        /// <param name="path">The request path for JSON-RPC methods.</param>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
        public static IApplicationBuilder UseJsonRpcService<T>(this IApplicationBuilder builder, PathString path = default)
            where T : class, IJsonRpcService
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.Map(path, b => b.UseMiddleware<JsonRpcMiddleware<JsonRpcServiceHandler<T>>>());
        }
    }
}