using System;
using System.Collections.Generic;
using Anemonis.AspNetCore.JsonRpc.UnitTests.TestStubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Anemonis.AspNetCore.JsonRpc.UnitTests
{
    [TestClass]
    public sealed class JsonRpcServicesExtensionsTests
    {
        [TestMethod]
        public void AddJsonRpcHandlerWithBuilderAndTypeWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(null, typeof(JsonRpcTestHandler2)));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithBuilderAndTypeWhenTypeInNull()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, null));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithBuilderAndTypeWhenTypeInNotClass()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, typeof(int)));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithBuilderAndTypeWhenTypeDoesNotImplementInterface()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, typeof(object)));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithBuilderAndType()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, typeof(JsonRpcTestHandler1));

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithBuilderWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler<JsonRpcTestHandler2>(null));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithBuilder()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcHandler<JsonRpcTestHandler1>(servicesMock.Object);

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcHandlersWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandlers(null));
        }

        [TestMethod]
        public void AddJsonRpcHandlers()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcHandlers(servicesMock.Object);
        }

        [TestMethod]
        public void AddJsonRpcServiceWithBuilderAndTypeWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(null, typeof(JsonRpcTestService1)));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithBuilderAndTypeWhenTypeInNull()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, null));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithBuilderAndTypeWhenTypeInNotClass()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, typeof(int)));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithBuilderAndTypeWhenTypeDoesNotImplementInterface()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, typeof(object)));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithBuilderAndType()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, typeof(JsonRpcTestService1));

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcServiceWithBuilderWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService<JsonRpcTestService1>(null));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithBuilder()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcService<JsonRpcTestService1>(servicesMock.Object);

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcServicesWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcServices(null));
        }

        [TestMethod]
        public void AddJsonRpcServices()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcServices(servicesMock.Object);
        }

        [TestMethod]
        public void AddJsonRpcWithOptionsWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpc(null, new JsonRpcOptions()));
        }

        [TestMethod]
        public void AddJsonRpcWithOptionsWhenOptionsInNull()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpc(servicesMock.Object, null));
        }

        [TestMethod]
        public void AddJsonRpcWithOptions()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.GetEnumerator())
                .Returns(new List<ServiceDescriptor>().GetEnumerator());
            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpc(servicesMock.Object, new JsonRpcOptions());
        }
    }
}