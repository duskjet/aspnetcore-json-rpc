using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Benchmarks.Middleware
{
    internal sealed class JsonRpcTestService : IJsonRpcService
    {
        [JsonRpcName("mn")]
        public Task<long> MethodWithParamsByName(
            [JsonRpcName("p1")]long parameter1,
            [JsonRpcName("p2")]long parameter2)
        {
            return Task.FromResult(3L);
        }

        [JsonRpcName("mp")]
        public Task<long> MethodWithParamsByPosition(
            long parameter1,
            long parameter2)
        {
            return Task.FromResult(3L);
        }

        [JsonRpcName("me")]
        public Task<long> MethodWithErrorResponse()
        {
            throw new JsonRpcServiceException(0L, "m");
        }

        [JsonRpcName("mt")]
        public Task MethodWithNotification()
        {
            return Task.CompletedTask;
        }
    }
}