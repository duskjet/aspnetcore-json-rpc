using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Benchmarks.Middleware
{
    internal sealed class JsonRpcTestService : IJsonRpcService
    {
        [JsonRpcMethod("mn", "p1", "p2")]
        public Task<long> MethodWithParametersByName(long parameter1, long parameter2)
        {
            return Task.FromResult(3L);
        }

        [JsonRpcMethod("mp", 0, 1)]
        public Task<long> MethodWithParametersByPosition(long parameter1, long parameter2)
        {
            return Task.FromResult(3L);
        }

        [JsonRpcMethod("me")]
        public Task<long> MethodWithErrorResponse()
        {
            throw new JsonRpcServiceException(0L, "m");
        }

        [JsonRpcMethod("mt")]
        public Task MethodWithNotification()
        {
            return Task.CompletedTask;
        }
    }
}