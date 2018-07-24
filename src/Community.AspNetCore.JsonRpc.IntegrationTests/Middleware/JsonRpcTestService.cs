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

        [JsonRpcName("nam")]
        public Task<long> MethodWithParametersByName(
            [JsonRpcName("p1")] long parameter1,
            [JsonRpcName("p2")] long parameter2)
        {
            Assert.AreEqual(1L, parameter1);
            Assert.AreEqual(2L, parameter2);

            return Task.FromResult(-1L);
        }

        [JsonRpcName("pos")]
        public Task<long> MethodWithParametersByPosition(
            long parameter1,
            long parameter2)
        {
            Assert.AreEqual(1L, parameter1);
            Assert.AreEqual(2L, parameter2);

            return Task.FromResult(3L);
        }

        [JsonRpcName("err")]
        public Task<long> MethodWithErrorResponse()
        {
            throw new JsonRpcServiceException(0L, "m");
        }

        [JsonRpcName("not")]
        public Task MethodWithNotification()
        {
            return Task.CompletedTask;
        }
    }
}