using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Tests
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public IReadOnlyDictionary<string, JsonRpcRequestContract> CreateScheme()
        {
            return new Dictionary<string, JsonRpcRequestContract>
            {
                ["pin"] = JsonRpcRequestContract.Default,
                ["clr"] = JsonRpcRequestContract.Default,
                ["add"] = new JsonRpcRequestContract(
                    new[]
                    {
                        typeof(long),
                        typeof(long)
                    }),
                ["sub"] = new JsonRpcRequestContract(
                    new Dictionary<string, Type>
                    {
                        ["o1"] = typeof(long),
                        ["o2"] = typeof(long)
                    })
            };
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