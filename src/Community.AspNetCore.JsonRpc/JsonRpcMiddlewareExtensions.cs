using System;
using System.Globalization;
using Community.AspNetCore.JsonRpc;
using Community.AspNetCore.JsonRpc.Resources;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>JSON-RPC middleware extensions for the <see cref="IApplicationBuilder" />.</summary>
    public static class JsonRpcMiddlewareExtensions
    {
        /// <summary>Registers a JSON-RPC 2.0 handler.</summary>
        /// <param name="builder">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="type">The type of the handler.<</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="args">The arguments to pass to the handler type instance's constructor.</param>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null" />.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="type" /> is invalid.</exception>
        public static IApplicationBuilder UseJsonRpcHandler(this IApplicationBuilder builder, Type type, PathString path = default, params object[] args)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (!type.IsClass || !typeof(IJsonRpcHandler).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Strings.GetString("middleware.invalid_type"), type));
            }

            return builder.Map(path, configuration => configuration.UseMiddleware(typeof(JsonRpcMiddleware<>).MakeGenericType(type), (object)args));
        }

        /// <summary>Registers a JSON-RPC 2.0 handler.</summary>
        /// <param name="builder">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="args">The arguments to pass to the handler type instance's constructor.</param>
        /// <typeparam name="T">The type of the handler.</typeparam>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        public static IApplicationBuilder UseJsonRpcHandler<T>(this IApplicationBuilder builder, PathString path = default, params object[] args)
            where T : class, IJsonRpcHandler
        {
            return builder.Map(path, configuration => configuration.UseMiddleware<JsonRpcMiddleware<T>>((object)args));
        }

        /// <summary>Registers a JSON-RPC 2.0 service.</summary>
        /// <param name="builder">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="type">The type of the service.<</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="args">The arguments to pass to the service type instance's constructor.</param>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type" /> is <see langword="null" />.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="type" /> is invalid.</exception>
        public static IApplicationBuilder UseJsonRpcService(this IApplicationBuilder builder, Type type, PathString path = default, params object[] args)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (!type.IsClass)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Strings.GetString("middleware.invalid_type"), type));
            }

            return builder.UseJsonRpcHandler(typeof(JsonRpcServiceHandler<>).MakeGenericType(type), path, (object)args);
        }

        /// <summary>Registers a JSON-RPC 2.0 service.</summary>
        /// <param name="builder">The <see cref="IApplicationBuilder" /> to configure.</param>
        /// <param name="path">The request path for JSON-RPC processing.</param>
        /// <param name="args">The arguments to pass to the service type instance's constructor.</param>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <returns>The <see cref="IApplicationBuilder" /> instance.</returns>
        public static IApplicationBuilder UseJsonRpcService<T>(this IApplicationBuilder builder, PathString path = default, params object[] args)
            where T : class
        {
            return builder.UseJsonRpcHandler<JsonRpcServiceHandler<T>>(path, (object)args);
        }
    }
}