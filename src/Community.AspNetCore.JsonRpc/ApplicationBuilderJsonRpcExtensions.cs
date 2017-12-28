using System;
using Community.AspNetCore.JsonRpc;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>JSON-RPC extensions for <see cref="IApplicationBuilder" />.</summary>
    public static class ApplicationBuilderJsonRpcExtensions
    {
        /// <summary>Use JSON-RPC 2.0 handler for the specified path.</summary>
        /// <param name="app">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="handler">The JSON-RPC handler.</param>
        /// <typeparam name="T">The type of the handler.</typeparam>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="app" />, <paramref name="path" />, or <paramref name="handler" /> is <see langword="null" />.</exception>
        public static IApplicationBuilder UseJsonRpcHandler<T>(this IApplicationBuilder app, PathString path, T handler)
            where T : class, IJsonRpcHandler
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return app.Map(path, configuration => configuration.UseMiddleware<JsonRpcMiddleware>(handler));
        }

        /// <summary>Use JSON-RPC 2.0 service for the specified path.</summary>
        /// <param name="app">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="service">The JSON-RPC service.</param>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="app" />, <paramref name="path" />, or <paramref name="service" /> is <see langword="null" />.</exception>
        public static IApplicationBuilder UseJsonRpcService<T>(this IApplicationBuilder app, PathString path, T service)
            where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            return app.UseJsonRpcHandler(path, new JsonRpcServiceHandler<T>(service));
        }
    }
}