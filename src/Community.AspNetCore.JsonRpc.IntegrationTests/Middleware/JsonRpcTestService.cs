using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.AspNetCore.JsonRpc.IntegrationTests.Middleware
{
    internal sealed class JsonRpcTestService : IJsonRpcService
    {
        public JsonRpcTestService(ILoggerFactory loggerFactory)
        {
            Assert.IsNotNull(loggerFactory);
        }

        [JsonRpcMethod("nam", "p1", "p2")]
        public Task<long> MethodWithParametersByName(long parameter1, long parameter2)
        {
            Assert.AreEqual(1L, parameter1);
            Assert.AreEqual(2L, parameter2);

            return Task.FromResult(-1L);
        }

        [JsonRpcMethod("pos", 0, 1)]
        public Task<long> MethodWithParametersByPosition(long parameter1, long parameter2)
        {
            Assert.AreEqual(1L, parameter1);
            Assert.AreEqual(2L, parameter2);

            return Task.FromResult(3L);
        }

        [JsonRpcMethod("err")]
        public Task<long> MethodWithErrorResponse()
        {
            throw new JsonRpcServiceException(0L, "m");
        }

        [JsonRpcMethod("not")]
        public Task MethodWithNotification()
        {
            return Task.CompletedTask;
        }
    }
}