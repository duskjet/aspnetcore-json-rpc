using System;
using Community.AspNetCore.JsonRpc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        /// <summary>Registers the specified JSON-RPC transport options via <see cref="IOptions{T}"/> in the <see cref="IServiceCollection" /> instance.</summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to register the options in.</param>
        /// <param name="options">The JSON-RPC transport options to use by JSON-RPC middleware.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serviceCollection" /> or <paramref name="options" /> is <see langword="null" />.</exception>
        public static IServiceCollection AddJsonRpcOptions(this IServiceCollection serviceCollection, JsonRpcOptions options)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return serviceCollection.Configure<JsonRpcOptions>(o => ApplyOptions(options, o));
        }

        private static void ApplyOptions(JsonRpcOptions source, JsonRpcOptions target)
        {
            target.MaxBatchSize = source.MaxBatchSize;
            target.MaxIdLength = source.MaxIdLength;
        }
    }
}