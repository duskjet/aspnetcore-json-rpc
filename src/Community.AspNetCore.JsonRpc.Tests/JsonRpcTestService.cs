using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Tests
{
    internal interface IJsonRpcTestService
    {
        [JsonRpcName("pin")]
        Task Ping();

        [JsonRpcName("clr")]
        Task Clear();

        [JsonRpcName("add")]
        Task<long> Add(long operand1, long operand2);

        [JsonRpcName("sub")]
        Task<long> Substract([JsonRpcName("o1")]long operand1, [JsonRpcName("o2")]long operand2);
    }

    internal sealed class JsonRpcTestService : IJsonRpcTestService
    {
        public Task Ping()
        {
            return Task.CompletedTask;
        }

        public Task Clear()
        {
            throw new JsonRpcServiceException(100L, "OPERATION_NOT_AVAILABLE");
        }

        public Task<long> Add(long operand1, long operand2)
        {
            return Task.FromResult(operand1 + operand2);
        }

        public Task<long> Substract(long operand1, long operand2)
        {
            return Task.FromResult(operand1 - operand2);
        }
    }
}