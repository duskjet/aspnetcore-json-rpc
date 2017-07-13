using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.FunctionalTests
{
    internal sealed class JsonRpcTestService : IJsonRpcService
    {
        [JsonRpcMethod("ac")]
        public Task AllClear()
        {
            return Task.CompletedTask;
        }

        [JsonRpcMethod("divide")]
        public Task<double> Divide(CalculatorOperands operands)
        {
            if (operands.Operand2 == 0)
            {
                throw new JsonRpcServiceException(100, "Operand 2 equals zero");
            }

            return Task.FromResult(operands.Operand1 / operands.Operand2);
        }

        [JsonRpcMethod("multiply")]
        public Task<double> Multiply(CalculatorOperands operands)
        {
            return Task.FromResult(operands.Operand1 * operands.Operand2);
        }

        [JsonRpcMethod("plus")]
        public Task<double> Plus(CalculatorOperands operands)
        {
            return Task.FromResult(operands.Operand1 + operands.Operand2);
        }

        [JsonRpcMethod("minus")]
        public Task<double> Minus(CalculatorOperands operands)
        {
            return Task.FromResult(operands.Operand1 - operands.Operand2);
        }

        [JsonRpcMethod("power")]
        public Task<double> Power(CalculatorOperands operands)
        {
            throw new JsonRpcException("Unknown error");
        }
    }

    internal sealed class JsonRpcTestServiceInvalidMethodName : IJsonRpcService
    {
        [JsonRpcMethod("method_a")]
        public Task MethodA()
        {
            return Task.CompletedTask;
        }

        [JsonRpcMethod("method_a")]
        public Task MethodB()
        {
            return Task.CompletedTask;
        }
    }


    internal sealed class JsonRpcTestServiceInvalidMethodReturnType : IJsonRpcService
    {
        [JsonRpcMethod("method_a")]
        public int MethodA()
        {
            return 42;
        }
    }

    internal sealed class JsonRpcTestServiceInvalidMethodParameters : IJsonRpcService
    {
        [JsonRpcMethod("method_a")]
        public Task MethodA(string paramA, string paramB)
        {
            return Task.CompletedTask;
        }
    }
}