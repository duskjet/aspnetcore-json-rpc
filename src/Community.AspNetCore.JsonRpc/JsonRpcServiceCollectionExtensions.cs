using Community.AspNetCore.JsonRpc;
using Microsoft.Extensions.DependencyInjection;

namespace Community.AspNetCore
{
    /// <summary>The JSON-RPC 2.0 middleware extensions for the <see cref="IServiceCollection" />.</summary>
    public static class JsonRpcServiceCollectionExtensions
    {
        /// <summary>Registers the specified JSON-RPC 2.0 handler in the <see cref="IServiceCollection" /> instance.</summary>
        /// <typeparam name="T">The type of the handler.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to register the middleware in.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IServiceCollection AddJsonRpcHandler<T>(this IServiceCollection services)
            where T : class, IJsonRpcHandler
        {
            return services.AddScoped<JsonRpcMiddleware<T>, JsonRpcMiddleware<T>>();
        }

        /// <summary>Registers the specified JSON-RPC 2.0 service in the <see cref="IServiceCollection" /> instance.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/> to register the middleware in.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IServiceCollection AddJsonRpcService<T>(this IServiceCollection services)
            where T : class
        {
            return services.AddJsonRpcHandler<JsonRpcServiceHandler<T>>();
        }
    }
}