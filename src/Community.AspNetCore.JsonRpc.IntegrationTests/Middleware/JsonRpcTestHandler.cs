using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.AspNetCore.JsonRpc.IntegrationTests.Middleware
{
    internal sealed class JsonRpcTestHandler : IJsonRpcHandler
    {
        public JsonRpcTestHandler(ILoggerFactory loggerFactory)
        {
            Assert.IsNotNull(loggerFactory);
        }

        public IReadOnlyDictionary<string, JsonRpcRequestContract> GetContracts()
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
            Assert.IsNotNull(request);
            Assert.IsFalse(request.IsSystem);

            var response = default(JsonRpcResponse);

            switch (request.Method)
            {
                case "nam":
                    {
                        Assert.AreEqual(JsonRpcParametersType.ByName, request.ParametersType);
                        Assert.AreEqual(2, request.ParametersByName.Count);
                        Assert.IsTrue(request.ParametersByName.ContainsKey("p1"));
                        Assert.IsTrue(request.ParametersByName.ContainsKey("p2"));
                        Assert.IsInstanceOfType(request.ParametersByName["p1"], typeof(long));
                        Assert.IsInstanceOfType(request.ParametersByName["p2"], typeof(long));
                        Assert.AreEqual(1L, (long)request.ParametersByName["p1"]);
                        Assert.AreEqual(2L, (long)request.ParametersByName["p2"]);

                        response = new JsonRpcResponse(-1L, request.Id);
                    }
                    break;
                case "pos":
                    {
                        Assert.AreEqual(JsonRpcParametersType.ByPosition, request.ParametersType);
                        Assert.AreEqual(2, request.ParametersByPosition.Count);
                        Assert.IsInstanceOfType(request.ParametersByPosition[0], typeof(long));
                        Assert.IsInstanceOfType(request.ParametersByPosition[1], typeof(long));
                        Assert.AreEqual(1L, (long)request.ParametersByPosition[0]);
                        Assert.AreEqual(2L, (long)request.ParametersByPosition[1]);

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