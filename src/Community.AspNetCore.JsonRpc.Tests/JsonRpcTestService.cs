using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Tests
{
    internal sealed class JsonRpcTestService
    {
        [JsonRpcName("pin")]
        public Task Ping()
        {
            return Task.CompletedTask;
        }

        [JsonRpcName("clr")]
        public Task Clear()
        {
            throw new JsonRpcServiceException(100L, "OPERATION_NOT_AVAILABLE");
        }

        [JsonRpcName("add")]
        public Task<long> Add(long operand1, long operand2)
        {
            return Task.FromResult(operand1 + operand2);
        }

        [JsonRpcName("sub")]
        public Task<long> Substract([JsonRpcName("o1")]long operand1, [JsonRpcName("o2")]long operand2)
        {
            return Task.FromResult(operand1 - operand2);
        }
    }
}