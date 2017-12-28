using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Tests
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public JsonRpcSerializerScheme CreateScheme()
        {
            var scheme = new JsonRpcSerializerScheme();

            scheme.Methods["ac"] = new JsonRpcMethodScheme();

            var paramsSchemeDivide = new Dictionary<string, Type>
            {
                ["operand_1"] = typeof(double),
                ["operand_2"] = typeof(double)
            };

            scheme.Methods["divide"] = new JsonRpcMethodScheme(paramsSchemeDivide);

            var paramsSchemeMinus = new[]
            {
                typeof(double),
                typeof(double)
            };

            scheme.Methods["minus"] = new JsonRpcMethodScheme(paramsSchemeMinus);

            return scheme;
        }

        public Task<JsonRpcResponse> Handle(JsonRpcRequest request)
        {
            var response = default(JsonRpcResponse);

            switch (request.Method)
            {
                case "ac":
                    {
                    }
                    break;
                case "divide":
                    {
                        var operand1 = (double)request.ParamsByName["operand_1"];
                        var operand2 = (double)request.ParamsByName["operand_2"];

                        if (operand2 == 0)
                        {
                            response = new JsonRpcResponse(new JsonRpcError(100, "Operand 2 equals zero"), request.Id);
                        }
                        else
                        {
                            var result = operand1 / operand2;

                            response = new JsonRpcResponse(result, request.Id);
                        }
                    }
                    break;
                case "minus":
                    {
                        var operand1 = (double)request.ParamsByPosition[0];
                        var operand2 = (double)request.ParamsByPosition[1];

                        var result = operand1 - operand2;

                        response = new JsonRpcResponse(result, request.Id);
                    }
                    break;
            }

            return Task.FromResult(response);
        }
    }
}