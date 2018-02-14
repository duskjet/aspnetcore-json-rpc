using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Community.AspNetCore.JsonRpc.Tests.Middleware
{
    internal sealed class JsonRpcTestService : IJsonRpcService
    {
        public JsonRpcTestService(ILoggerFactory loggerFactory)
        {
            Assert.NotNull(loggerFactory);
        }

        [JsonRpcName("nam")]
        public Task<long> MethodWithParamsByName(
            [JsonRpcName("p1")] long parameter1,
            [JsonRpcName("p2")] long parameter2)
        {
            Assert.Equal(1L, parameter1);
            Assert.Equal(2L, parameter2);

            return Task.FromResult(-1L);
        }

        [JsonRpcName("pos")]
        public Task<long> MethodWithParamsByPosition(
            long parameter1,
            long parameter2)
        {
            Assert.Equal(1L, parameter1);
            Assert.Equal(2L, parameter2);

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