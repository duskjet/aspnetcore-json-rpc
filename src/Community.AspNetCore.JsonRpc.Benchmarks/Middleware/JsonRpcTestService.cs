using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Benchmarks.Middleware
{
    internal sealed class JsonRpcTestService
    {
        [JsonRpcName("nam")]
        public Task<long> MethodWithParamsByName([JsonRpcName("pr1")]long parameter1, [JsonRpcName("pr2")]long parameter2)
        {
            return Task.FromResult(parameter1 - parameter2);
        }

        [JsonRpcName("pos")]
        public Task<long> MethodWithParamsByPosition(long parameter1, long parameter2)
        {
            return Task.FromResult(parameter1 + parameter2);
        }

        [JsonRpcName("err")]
        public Task<long> MethodWithErrorResponse()
        {
            throw new JsonRpcServiceException(100L, "94cccbe7-d613-4aca-8940-9298892b8ee6");
        }

        [JsonRpcName("not")]
        public Task MethodWithNotification()
        {
            return Task.CompletedTask;
        }
    }
}