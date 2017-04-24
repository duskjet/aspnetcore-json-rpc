using System;
using System.Data.JsonRpc;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Community.AspNetCore.JsonRpc.FunctionalTests
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public JsonRpcSerializerScheme CreateScheme()
        {
            var result = new JsonRpcSerializerScheme();

            result.Methods["divide"] = new JsonRpcMethodScheme(typeof(CalculatorOperands));
            result.Methods["multiply"] = new JsonRpcMethodScheme(typeof(CalculatorOperands));
            result.Methods["plus"] = new JsonRpcMethodScheme(typeof(CalculatorOperands));
            result.Methods["minus"] = new JsonRpcMethodScheme(typeof(CalculatorOperands));
            result.Methods["power"] = new JsonRpcMethodScheme(typeof(CalculatorOperands));

            return result;
        }

        public Task HandleNotification(JsonRpcRequest request)
        {
            throw new InvalidOperationException("Notifications are not supported");
        }

        public Task<JsonRpcResponse> HandleRequest(JsonRpcRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

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
                        throw new InvalidOperationException("The operation is not supported at the moment");
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
                        throw new InvalidOperationException($"Unsupported operation: \"{request.Method}\"");
                    }
            }

            return Task.FromResult(response);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal struct CalculatorOperands
    {
        [JsonProperty("operand_1")]
        public double Operand1 { get; set; }

        [JsonProperty("operand_2")]
        public double Operand2 { get; set; }
    }
}