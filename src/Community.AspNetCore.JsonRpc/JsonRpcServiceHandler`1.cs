using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Community.AspNetCore.JsonRpc.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcServiceHandler<T> : IJsonRpcHandler, IDisposable
        where T : class, IJsonRpcService
    {
        private static readonly IReadOnlyDictionary<string, (MethodInfo, ParameterInfo[], string[])> _metadata;
        private static readonly IReadOnlyDictionary<string, JsonRpcRequestContract> _scheme;

        private readonly T _service;
        private readonly bool _dispose;

        static JsonRpcServiceHandler()
        {
            var blueprint = new Dictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])>(StringComparer.Ordinal);

            AcquireContracts(blueprint, typeof(T));

            var metadata = new Dictionary<string, (MethodInfo, ParameterInfo[], string[])>(blueprint.Count, StringComparer.Ordinal);
            var scheme = new Dictionary<string, JsonRpcRequestContract>(blueprint.Count, StringComparer.Ordinal);

            foreach (var kvp in blueprint)
            {
                var (contract, method, parameters, parametersBindings) = kvp.Value;

                metadata[kvp.Key] = (method, parameters, parametersBindings);
                scheme[kvp.Key] = contract;
            }

            _metadata = metadata;
            _scheme = scheme;
        }

        public JsonRpcServiceHandler(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _service = serviceProvider.GetService<T>();

            if (_service == null)
            {
                _service = ActivatorUtilities.CreateInstance<T>(serviceProvider);
                _dispose = _service is IDisposable;
            }
        }

        private static void AcquireContracts(IDictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])> blueprint, Type type)
        {
            if (type == null)
            {
                return;
            }

            AcquireContracts(blueprint, type.GetMethods(BindingFlags.Instance | BindingFlags.Public));
            AcquireContracts(blueprint, type.BaseType);

            foreach (var interfaceType in type.GetInterfaces())
            {
                AcquireContracts(blueprint, interfaceType);
            }
        }

        private static void AcquireContracts(IDictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])> blueprint, IEnumerable<MethodInfo> methods)
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
                if (blueprint.ContainsKey(methodNameAttribute.Value))
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.invalid_name"), typeof(T), methodNameAttribute.Value));
                }

                var methodContract = default(JsonRpcRequestContract);
                var parameters = method.GetParameters();
                var parametersBindings = default(string[]);

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

                        parametersBindings = new string[parameters.Length];

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
                            parametersBindings[i] = parameterNameAttribute.Value;
                        }

                        methodContract = new JsonRpcRequestContract(parametersContract);
                    }
                }
                else
                {
                    methodContract = new JsonRpcRequestContract();
                }

                blueprint[methodNameAttribute.Value] = (methodContract, method, parameters, parametersBindings);
            }
        }

        IReadOnlyDictionary<string, JsonRpcRequestContract> IJsonRpcHandler.CreateScheme()
        {
            return _scheme;
        }

        async Task<JsonRpcResponse> IJsonRpcHandler.HandleAsync(JsonRpcRequest request)
        {
            var (method, parameters, parametersBindings) = _metadata[request.Method];
            var parametersValues = default(object[]);

            switch (request.ParamsType)
            {
                case JsonRpcParamsType.ByPosition:
                    {
                        parametersValues = new object[parameters.Length];

                        for (var i = 0; i < parametersValues.Length; i++)
                        {
                            parametersValues[i] = request.ParamsByPosition[i];
                        }
                    }
                    break;
                case JsonRpcParamsType.ByName:
                    {
                        parametersValues = new object[parameters.Length];

                        for (var i = 0; i < parametersValues.Length; i++)
                        {
                            if (!request.ParamsByName.TryGetValue(parametersBindings[i], out parametersValues[i]))
                            {
                                if (parameters[i].HasDefaultValue)
                                {
                                    parametersValues[i] = parameters[i].DefaultValue;
                                }
                                else
                                {
                                    if (request.IsNotification)
                                    {
                                        return null;
                                    }

                                    var message = string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.request.parameter.undefined_value"), request.Method, parametersBindings[i]);

                                    return new JsonRpcResponse(new JsonRpcError((long)JsonRpcErrorType.InvalidParams, message), request.Id);
                                }
                            }
                        }
                    }
                    break;
            }

            try
            {
                if (request.IsNotification || !method.ReturnType.IsGenericType)
                {
                    await ((dynamic)method.Invoke(_service, parametersValues)).ConfigureAwait(false);
                }
                else
                {
                    return new JsonRpcResponse(await ((dynamic)method.Invoke(_service, parametersValues)).ConfigureAwait(false) as object, request.Id);
                }
            }
            catch (TargetInvocationException ex)
                when (ex.InnerException is OperationCanceledException iex)
            {
                ExceptionDispatchInfo.Capture(iex).Throw();
            }
            catch (TargetInvocationException ex)
                when (ex.InnerException is JsonRpcServiceException iex)
            {
                return new JsonRpcResponse(new JsonRpcError(iex.Code, iex.Message, iex.ErrorData), request.Id);
            }

            return null;
        }

        void IDisposable.Dispose()
        {
            if (_dispose)
            {
                ((IDisposable)_service).Dispose();
            }
        }
    }
}