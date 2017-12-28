using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Tests
{
    internal sealed class JsonRpcTestService
    {
        [JsonRpcName("ac")]
        public Task AllClear()
        {
            return Task.CompletedTask;
        }

        [JsonRpcName("divide")]
        public Task<double> Divide([JsonRpcName("operand_1")]double operand1, [JsonRpcName("operand_2")]double operand2)
        {
            if (operand2 == 0)
            {
                throw new JsonRpcServiceException(100, "Operand 2 equals zero");
            }

            return Task.FromResult(operand1 / operand2);
        }

        [JsonRpcName("minus")]
        public Task<double> Minus(double operand1, double operand2)
        {
            return Task.FromResult(operand1 - operand2);
        }
    }
}