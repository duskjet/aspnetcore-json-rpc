using System;
using Community.AspNetCore.JsonRpc;
using Microsoft.Extensions.DependencyInjection;

namespace Community.AspNetCore
{
    /// <summary>The JSON-RPC 2.0 middleware extensions for the <see cref="IServiceCollection" />.</summary>
    public static class JsonRpcServiceCollectionExtensions
    {
        /// <summary>Registers the specified JSON-RPC 2.0 handler in the <see cref="IServiceCollection" /> instance.</summary>
        /// <typeparam name="T">The type of the handler.</typeparam>
        /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to register the middleware in.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serviceCollection" /> is <see langword="null" />.</exception>
        public static IServiceCollection AddJsonRpcHandler<T>(this IServiceCollection serviceCollection)
            where T : class, IJsonRpcHandler
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            return serviceCollection.AddScoped<JsonRpcMiddleware<T>, JsonRpcMiddleware<T>>();
        }

        /// <summary>Registers the specified JSON-RPC 2.0 service in the <see cref="IServiceCollection" /> instance.</summary>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to register the middleware in.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serviceCollection" /> is <see langword="null" />.</exception>
        public static IServiceCollection AddJsonRpcService<T>(this IServiceCollection serviceCollection)
            where T : class, IJsonRpcService
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            return serviceCollection.AddScoped<JsonRpcMiddleware<JsonRpcServiceHandler<T>>, JsonRpcMiddleware<JsonRpcServiceHandler<T>>>();
        }
    }
}