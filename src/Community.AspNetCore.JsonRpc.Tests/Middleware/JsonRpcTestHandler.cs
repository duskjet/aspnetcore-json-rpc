using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc.Tests.Middleware
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public IReadOnlyDictionary<string, JsonRpcRequestContract> CreateScheme()
        {
            return new Dictionary<string, JsonRpcRequestContract>(StringComparer.Ordinal)
            {
                ["nam"] = new JsonRpcRequestContract(
                    new Dictionary<string, Type>
                    {
                        ["pr1"] = typeof(long),
                        ["pr2"] = typeof(long)
                    }),
                ["pos"] = new JsonRpcRequestContract(
                    new[]
                    {
                        typeof(long),
                        typeof(long)
                    }),
                ["err"] = JsonRpcRequestContract.Default,
                ["not"] = JsonRpcRequestContract.Default
            };
        }

        public Task<JsonRpcResponse> Handle(JsonRpcRequest request)
        {
            var response = default(JsonRpcResponse);

            switch (request.Method)
            {
                case "nam":
                    {
                        var parameter1 = (long)request.ParamsByName["pr1"];
                        var parameter2 = (long)request.ParamsByName["pr2"];

                        response = new JsonRpcResponse(parameter1 - parameter2, request.Id);
                    }
                    break;
                case "pos":
                    {
                        var parameter1 = (long)request.ParamsByPosition[0];
                        var parameter2 = (long)request.ParamsByPosition[1];

                        response = new JsonRpcResponse(parameter1 + parameter2, request.Id);
                    }
                    break;
                case "err":
                    {
                        var error = new JsonRpcError(100L, "94cccbe7-d613-4aca-8940-9298892b8ee6");

                        response = new JsonRpcResponse(error, request.Id);
                    }
                    break;
                case "not":
                    {
                    }
                    break;
            }

            return Task.FromResult(response);
        }
    }
}