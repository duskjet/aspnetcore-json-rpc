using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Anemonis.AspNetCore.JsonRpc.UnitTests
{
    [TestClass]
    public sealed class JsonRpcServiceExceptionTests
    {
        [TestMethod]
        public void ConstructorWithCodeAndMessageWhenMessageIsNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                new JsonRpcServiceException(default, null));
        }

        [TestMethod]
        public void ConstructorWithCodeAndMessage()
        {
            new JsonRpcServiceException(default, "m");
        }
    }
}