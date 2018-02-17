using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Community.AspNetCore.JsonRpc.Tests.Middleware
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public JsonRpcTestHandler(ILoggerFactory loggerFactory)
        {
            Assert.NotNull(loggerFactory);
        }

        public IReadOnlyDictionary<string, JsonRpcRequestContract> CreateScheme()
        {
            return new Dictionary<string, JsonRpcRequestContract>(StringComparer.Ordinal)
            {
                ["nam"] = new JsonRpcRequestContract(
                    new Dictionary<string, Type>
                    {
                        ["p1"] = typeof(long),
                        ["p2"] = typeof(long)
                    }),
                ["pos"] = new JsonRpcRequestContract(
                    new[]
                    {
                        typeof(long),
                        typeof(long)
                    }),
                ["err"] = new JsonRpcRequestContract(),
                ["not"] = new JsonRpcRequestContract()
            };
        }

        public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request)
        {
            Assert.NotNull(request);
            Assert.False(request.IsSystem);

            var response = default(JsonRpcResponse);

            switch (request.Method)
            {
                case "nam":
                    {
                        Assert.Equal(JsonRpcParametersType.ByName, request.ParametersType);
                        Assert.Equal(2, request.ParametersByName.Count);
                        Assert.True(request.ParametersByName.ContainsKey("p1"));
                        Assert.True(request.ParametersByName.ContainsKey("p2"));
                        Assert.IsType<long>(request.ParametersByName["p1"]);
                        Assert.IsType<long>(request.ParametersByName["p2"]);
                        Assert.Equal(1L, (long)request.ParametersByName["p1"]);
                        Assert.Equal(2L, (long)request.ParametersByName["p2"]);

                        response = new JsonRpcResponse(-1L, request.Id);
                    }
                    break;
                case "pos":
                    {
                        Assert.Equal(JsonRpcParametersType.ByPosition, request.ParametersType);
                        Assert.Equal(2, request.ParametersByPosition.Count);
                        Assert.IsType<long>(request.ParametersByPosition[0]);
                        Assert.IsType<long>(request.ParametersByPosition[1]);
                        Assert.Equal(1L, (long)request.ParametersByPosition[0]);
                        Assert.Equal(2L, (long)request.ParametersByPosition[1]);

                        response = new JsonRpcResponse(3L, request.Id);
                    }
                    break;
                case "err":
                    {
                        response = new JsonRpcResponse(new JsonRpcError(0L, "m", null), request.Id);
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