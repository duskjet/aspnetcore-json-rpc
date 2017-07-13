using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.FunctionalTests
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public JsonRpcSerializerScheme CreateScheme()
        {
            var result = new JsonRpcSerializerScheme();

            result.Methods["ac"] = new JsonRpcMethodScheme(true);
            result.Methods["divide"] = new JsonRpcMethodScheme(false, typeof(CalculatorOperands));
            result.Methods["multiply"] = new JsonRpcMethodScheme(false, typeof(CalculatorOperands));
            result.Methods["plus"] = new JsonRpcMethodScheme(false, typeof(CalculatorOperands));
            result.Methods["minus"] = new JsonRpcMethodScheme(false, typeof(CalculatorOperands));
            result.Methods["power"] = new JsonRpcMethodScheme(false, typeof(CalculatorOperands));

            return result;
        }

        public Task HandleNotification(JsonRpcRequest request)
        {
            switch (request.Method)
            {
                case "ac":
                    {
                        return Task.CompletedTask;
                    }
                default:
                    {
                        throw new JsonRpcException($"Unsupported operation: \"{request.Method}\"");
                    }
            }
        }

        public Task<JsonRpcResponse> HandleRequest(JsonRpcRequest request)
        {
            var operands = (CalculatorOperands)request.Params;
            var response = default(JsonRpcResponse);

            switch (request.Method)
            {
                case "divide":
                    {
                        response = operands.Operand2 != 0 ?
                            new JsonRpcResponse(operands.Operand1 / operands.Operand2, request.Id) :
                            new JsonRpcResponse(new JsonRpcError(100, "Operand 2 equals zero"), request.Id);
                    }
                    break;
                case "multiply":
                    {
                        throw new JsonRpcException("The operation is not supported at the moment");
                    }
                case "plus":
                    {
                        response = new JsonRpcResponse(operands.Operand1 + operands.Operand2, request.Id);
                    }
                    break;
                case "minus":
                    {
                        response = new JsonRpcResponse(operands.Operand1 - operands.Operand2, JsonRpcId.None);
                    }
                    break;
                case "power":
                    {
                    }
                    break;
                default:
                    {
                        throw new JsonRpcException($"Unsupported operation: \"{request.Method}\"");
                    }
            }

            return Task.FromResult(response);
        }
    }
}