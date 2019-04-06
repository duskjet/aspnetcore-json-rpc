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
        public void AddJsonRpcHandlerWithServicesAndTypeWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(null, typeof(JsonRpcTestHandler2)));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithServicesAndTypeWhenTypeInNull()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, null));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithServicesAndTypeWhenTypeInNotClass()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, typeof(int)));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithServicesAndTypeWhenTypeDoesNotImplementInterface()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, typeof(object)));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithServicesAndType()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcHandler(servicesMock.Object, typeof(JsonRpcTestHandler1));

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithServicesWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandler<JsonRpcTestHandler2>(null));
        }

        [TestMethod]
        public void AddJsonRpcHandlerWithServices()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcHandler<JsonRpcTestHandler1>(servicesMock.Object);

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcHandlersWithServicesWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcHandlers(null));
        }

        [TestMethod]
        public void AddJsonRpcHandlersWithServices()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcHandlers(servicesMock.Object);
        }

        [TestMethod]
        public void AddJsonRpcServiceWithServicesAndTypeWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(null, typeof(JsonRpcTestService1)));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithServicesAndTypeWhenTypeInNull()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, null));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithServicesAndTypeWhenTypeInNotClass()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, typeof(int)));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithServicesAndTypeWhenTypeDoesNotImplementInterface()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, typeof(object)));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithServicesAndType()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcService(servicesMock.Object, typeof(JsonRpcTestService1));

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcServiceWithServicesWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcService<JsonRpcTestService1>(null));
        }

        [TestMethod]
        public void AddJsonRpcServiceWithServices()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcService<JsonRpcTestService1>(servicesMock.Object);

            servicesMock.Verify(o => o.Add(It.IsNotNull<ServiceDescriptor>()), Times.Once());
        }

        [TestMethod]
        public void AddJsonRpcServicesWithServicesWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpcServices(null));
        }

        [TestMethod]
        public void AddJsonRpcServicesWithServices()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpcServices(servicesMock.Object);
        }

        [TestMethod]
        public void AddJsonRpcWithWithServicesAndOptionsWhenBuilderInNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpc(null, new JsonRpcOptions()));
        }

        [TestMethod]
        public void AddJsonRpcWithWithServicesAndOptionsWhenOptionsInNull()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            Assert.ThrowsException<ArgumentNullException>(() =>
                JsonRpcServicesExtensions.AddJsonRpc(servicesMock.Object, null));
        }

        [TestMethod]
        public void AddJsonRpcWithWithServicesAndOptions()
        {
            var servicesMock = new Mock<IServiceCollection>(MockBehavior.Strict);

            servicesMock.Setup(o => o.GetEnumerator())
                .Returns(new List<ServiceDescriptor>().GetEnumerator());
            servicesMock.Setup(o => o.Add(It.IsNotNull<ServiceDescriptor>()));

            JsonRpcServicesExtensions.AddJsonRpc(servicesMock.Object, new JsonRpcOptions());
        }
    }
}