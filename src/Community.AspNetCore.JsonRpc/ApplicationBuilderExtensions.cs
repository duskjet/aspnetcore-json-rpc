using System;
using Community.AspNetCore.JsonRpc;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>JSON-RPC extensions for <see cref="IApplicationBuilder" />.</summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>Use JSON-RPC 2.0 handler for the specified path.</summary>
        /// <param name="app">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="handler">The JSON-RPC handler.</param>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="app" />, <paramref name="path" />, or <paramref name="handler" /> is <see langword="null" />.</exception>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder app, PathString path, IJsonRpcHandler handler)
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

            return app.Map(path, x => x.UseMiddleware<JsonRpcMiddleware>(handler));
        }

        /// <summary>Use JSON-RPC 2.0 service for the specified path.</summary>
        /// <param name="app">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="service">The JSON-RPC handler.</param>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="app" />, <paramref name="path" />, or <paramref name="service" /> is <see langword="null" />.</exception>
        public static IApplicationBuilder UseJsonRpc(this IApplicationBuilder app, PathString path, IJsonRpcService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            return app.UseJsonRpc(path, new JsonRpcServiceHandler(service));
        }
    }
}