using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcServiceHandler<T> : IJsonRpcHandler
        where T : class
    {
        private readonly Dictionary<string, (MethodInfo, string[])> _methods = new Dictionary<string, (MethodInfo, string[])>(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonRpcRequestContract> _scheme = new Dictionary<string, JsonRpcRequestContract>(StringComparer.Ordinal);
        private readonly T _service;

        public JsonRpcServiceHandler(IServiceProvider serviceProvider, object args)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _service = ActivatorUtilities.CreateInstance<T>(serviceProvider, (object[])args);

            var outcome = new Dictionary<string, (JsonRpcRequestContract, MethodInfo, string[])>(StringComparer.Ordinal);

            AcquireContracts(outcome, typeof(T));

            if (outcome.Count == 0)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, Strings.GetString("handler.empty_scheme"), typeof(T)));
            }

            foreach (var kvp in outcome)
            {
                var (contract, method, parametersPositions) = kvp.Value;

                _methods[kvp.Key] = (method, parametersPositions);
                _scheme[kvp.Key] = contract;
            }
        }

        private static void AcquireContracts(IDictionary<string, (JsonRpcRequestContract, MethodInfo, string[])> contracts, Type type)
        {
            if (type == null)
            {
                return;
            }

            AcquireContracts(contracts, type.GetMethods(BindingFlags.Instance | BindingFlags.Public));
            AcquireContracts(contracts, type.BaseType);

            foreach (var interfaceType in type.GetInterfaces())
            {
                AcquireContracts(contracts, interfaceType);
            }
        }

        private static void AcquireContracts(IDictionary<string, (JsonRpcRequestContract, MethodInfo, string[])> scheme, IEnumerable<MethodInfo> methods)
        {
            foreach (var method in methods)
            {
                var methodNameAttribute = method.GetCustomAttribute<JsonRpcNameAttribute>();

                if (methodNameAttribute == null)
                {
                    continue;
                }
                if (!(method.ReturnType == typeof(Task)) && !(method.ReturnType.IsGenericType && (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))))
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.invalid_type"), method.Name, typeof(T)));
                }
                if (scheme.ContainsKey(methodNameAttribute.Value))
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.invalid_name"), typeof(T), methodNameAttribute.Value));
                }

                var methodContract = JsonRpcRequestContract.Default;
                var parameters = method.GetParameters();
                var parametersPositions = default(string[]);

                if (parameters.Length > 0)
                {
                    if (parameters[0].GetCustomAttribute<JsonRpcNameAttribute>() == null)
                    {
                        var parametersContract = new Type[parameters.Length];

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            if (parameters[i].GetCustomAttribute<JsonRpcNameAttribute>() != null)
                            {
                                throw new InvalidOperationException(
                                    string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.parameter.unexpected_name"), method.Name, typeof(T), parameters[i].Name));
                            }

                            parametersContract[i] = parameters[i].ParameterType;
                        }

                        methodContract = new JsonRpcRequestContract(parametersContract);
                    }
                    else
                    {
                        var parametersContract = new Dictionary<string, Type>(parameters.Length, StringComparer.Ordinal);

                        parametersPositions = new string[parameters.Length];

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            var parameterNameAttribute = parameters[i].GetCustomAttribute<JsonRpcNameAttribute>();

                            if (parameterNameAttribute == null)
                            {
                                throw new InvalidOperationException(
                                    string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.parameter.undefined_name"), method.Name, typeof(T), parameters[i].Name));
                            }
                            if (parametersContract.ContainsKey(parameterNameAttribute.Value))
                            {
                                throw new InvalidOperationException(
                                    string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.parameter.invalid_name"), method.Name, typeof(T), parameterNameAttribute.Value));

                            }

                            parametersContract[parameterNameAttribute.Value] = parameters[i].ParameterType;
                            parametersPositions[i] = parameterNameAttribute.Value;
                        }

                        methodContract = new JsonRpcRequestContract(parametersContract);
                    }
                }

                scheme[methodNameAttribute.Value] = (methodContract, method, parametersPositions);
            }
        }

        IReadOnlyDictionary<string, JsonRpcRequestContract> IJsonRpcHandler.CreateScheme()
        {
            return _scheme;
        }

        async Task<JsonRpcResponse> IJsonRpcHandler.Handle(JsonRpcRequest request)
        {
            var (method, parametersPositions) = _methods[request.Method];
            var parameters = default(object[]);

            switch (request.ParamsType)
            {
                case JsonRpcParamsType.ByPosition:
                    {
                        parameters = new object[request.ParamsByPosition.Count];

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            parameters[i] = request.ParamsByPosition[i];
                        }
                    }
                    break;
                case JsonRpcParamsType.ByName:
                    {
                        parameters = new object[parametersPositions.Length];

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            if (!request.ParamsByName.TryGetValue(parametersPositions[i], out var parameterValue))
                            {
                                var error = new JsonRpcError(-32602L, string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.request.parameter.undefined_value"), parametersPositions[i], request.Method));

                                return new JsonRpcResponse(error, request.Id);
                            }

                            parameters[i] = parameterValue;
                        }
                    }
                    break;
                default:
                    {
                        parameters = Array.Empty<object>();
                    }
                    break;
            }

            if (request.IsNotification || !method.ReturnType.IsGenericType)
            {
                try
                {
                    await ((Task)method.Invoke(_service, parameters)).ConfigureAwait(false);
                }
                catch (TargetInvocationException ex)
                    when (ex.InnerException is JsonRpcServiceException iex)
                {
                    var error = new JsonRpcError(iex.Code, iex.Message, iex.RpcData);

                    return new JsonRpcResponse(error, request.Id);
                }

                return null;
            }
            else
            {
                try
                {
                    var result = await ((dynamic)method.Invoke(_service, parameters)).ConfigureAwait(false) as object;

                    return new JsonRpcResponse(result, request.Id);
                }
                catch (TargetInvocationException ex)
                    when (ex.InnerException is JsonRpcServiceException iex)
                {
                    var error = new JsonRpcError(iex.Code, iex.Message, iex.RpcData);

                    return new JsonRpcResponse(error, request.Id);
                }
            }
        }
    }
}