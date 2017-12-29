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

            scheme.Methods["pin"] = new JsonRpcMethodScheme();
            scheme.Methods["clr"] = new JsonRpcMethodScheme();
            scheme.Methods["add"] = new JsonRpcMethodScheme(
                new[]
                {
                    typeof(long),
                    typeof(long)
                });
            scheme.Methods["sub"] = new JsonRpcMethodScheme(
                new Dictionary<string, Type>
                {
                    ["o1"] = typeof(long),
                    ["o2"] = typeof(long)
                });

            return scheme;
        }

        public Task<JsonRpcResponse> Handle(JsonRpcRequest request)
        {
            var response = default(JsonRpcResponse);

            switch (request.Method)
            {
                case "clr":
                    {
                        var error = new JsonRpcError(100L, "OPERATION_NOT_AVAILABLE");

                        response = new JsonRpcResponse(error, request.Id);
                    }
                    break;
                case "add":
                    {
                        var operand1 = (long)request.ParamsByPosition[0];
                        var operand2 = (long)request.ParamsByPosition[1];

                        response = new JsonRpcResponse(operand1 + operand2, request.Id);
                    }
                    break;
                case "sub":
                    {
                        var operand1 = (long)request.ParamsByName["o1"];
                        var operand2 = (long)request.ParamsByName["o2"];

                        response = new JsonRpcResponse(operand1 - operand2, request.Id);
                    }
                    break;
            }

            return Task.FromResult(response);
        }
    }
}