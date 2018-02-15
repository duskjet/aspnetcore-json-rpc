using System;
using Community.AspNetCore.JsonRpc;
using Microsoft.Extensions.DependencyInjection;

namespace Community.AspNetCore
{
    /// <summary>The JSON-RPC 2.0 middleware extensions for the <see cref="IServiceCollection" />.</summary>
    public static class JsonRpcServicesExtensions
    {
        /// <summary>Adds the specified JSON-RPC 2.0 handler to the current <see cref="IServiceCollection" /> instance.</summary>
        /// <typeparam name="T">The type of the handler.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection" /> instance to add the handler to.</param>
        /// <param name="options">The middleware options to add to the current <see cref="IServiceCollection" /> instance.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
        public static IServiceCollection AddJsonRpcHandler<T>(this IServiceCollection services, JsonRpcOptions options = null)
            where T : class, IJsonRpcHandler
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (options != null)
            {
                services.Configure<JsonRpcOptions>(o => ApplyOptions(options, o));
            }

            services.AddScoped<JsonRpcMiddleware<T>, JsonRpcMiddleware<T>>();

            return services;
        }

        /// <summary>Adds the specified JSON-RPC 2.0 service to the current <see cref="IServiceCollection" /> instance.</summary>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection" /> instance to add the service to.</param>
        /// <param name="options">The middleware options to add to the current <see cref="IServiceCollection" /> instance.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
        public static IServiceCollection AddJsonRpcService<T>(this IServiceCollection services, JsonRpcOptions options = null)
            where T : class, IJsonRpcService
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (options != null)
            {
                services.Configure<JsonRpcOptions>(o => ApplyOptions(options, o));
            }

            services.AddScoped<JsonRpcMiddleware<JsonRpcServiceHandler<T>>, JsonRpcMiddleware<JsonRpcServiceHandler<T>>>();

            return services;
        }

        private static void ApplyOptions(JsonRpcOptions source, JsonRpcOptions target)
        {
            target.MaxBatchSize = source.MaxBatchSize;
            target.MaxIdLength = source.MaxIdLength;
        }
    }
}