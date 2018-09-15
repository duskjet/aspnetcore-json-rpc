using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Anemonis.JsonRpc;

namespace Anemonis.AspNetCore.JsonRpc.Benchmarks.Middleware
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public IReadOnlyDictionary<string, JsonRpcRequestContract> GetContracts()
        {
            return new Dictionary<string, JsonRpcRequestContract>(StringComparer.Ordinal)
            {
                ["mn"] = new JsonRpcRequestContract(
                    new Dictionary<string, Type>
                    {
                        ["p1"] = typeof(long),
                        ["p2"] = typeof(long)
                    }),
                ["mp"] = new JsonRpcRequestContract(
                    new[]
                    {
                        typeof(long),
                        typeof(long)
                    }),
                ["me"] = new JsonRpcRequestContract(),
                ["mt"] = new JsonRpcRequestContract()
            };
        }

        public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request)
        {
            var response = default(JsonRpcResponse);

            switch (request.Method)
            {
                case "mn":
                    {
                        response = new JsonRpcResponse(3L, request.Id);
                    }
                    break;
                case "mp":
                    {
                        response = new JsonRpcResponse(3L, request.Id);
                    }
                    break;
                case "me":
                    {
                        response = new JsonRpcResponse(new JsonRpcError(0L, "m"), request.Id);
                    }
                    break;
                case "mt":
                    {
                    }
                    break;
            }

            return Task.FromResult(response);
        }
    }
}