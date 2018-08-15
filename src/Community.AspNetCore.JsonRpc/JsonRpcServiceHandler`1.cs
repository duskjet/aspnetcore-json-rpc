// © Alexander Kozlenko. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Linq;
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
        private static readonly IReadOnlyDictionary<string, JsonRpcRequestContract> _contracts;

        private readonly T _service;

        static JsonRpcServiceHandler()
        {
            var blueprint = new Dictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])>(StringComparer.Ordinal);

            GetContracts(blueprint, typeof(T));

            var metadata = new Dictionary<string, (MethodInfo, ParameterInfo[], string[])>(blueprint.Count, StringComparer.Ordinal);
            var contracts = new Dictionary<string, JsonRpcRequestContract>(blueprint.Count, StringComparer.Ordinal);

            foreach (var kvp in blueprint)
            {
                var (contract, method, parameters, parametersBindings) = kvp.Value;

                metadata[kvp.Key] = (method, parameters, parametersBindings);
                contracts[kvp.Key] = contract;
            }

            _metadata = metadata;
            _contracts = contracts;
        }

        public JsonRpcServiceHandler(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _service = serviceProvider.GetService<T>() ?? ActivatorUtilities.CreateInstance<T>(serviceProvider);
        }

        private static void GetContracts(IDictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])> blueprint, Type type)
        {
            if (type == null)
            {
                return;
            }

            GetContracts(blueprint, type.GetMethods(BindingFlags.Instance | BindingFlags.Public));
            GetContracts(blueprint, type.BaseType);

            var interfaceTypes = type.GetInterfaces();

            for (var i = 0; i < interfaceTypes.Length; i++)
            {
                GetContracts(blueprint, interfaceTypes[i]);
            }
        }

        private static void GetContracts(IDictionary<string, (JsonRpcRequestContract, MethodInfo, ParameterInfo[], string[])> contracts, IEnumerable<MethodInfo> methods)
        {
            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<JsonRpcMethodAttribute>();

                if (attribute == null)
                {
                    continue;
                }
                if (!(method.ReturnType == typeof(Task)) && !(method.ReturnType.IsGenericType && (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))))
                {
                    throw new InvalidOperationException(string.Format(Strings.GetString("service.method.invalid_type"), method.Name, typeof(T)));
                }
                if (contracts.ContainsKey(attribute.MethodName))
                {
                    throw new InvalidOperationException(string.Format(Strings.GetString("service.method.invalid_name"), typeof(T), attribute.MethodName));
                }

                var contract = default(JsonRpcRequestContract);
                var parameters = method.GetParameters();
                var parametersBindings = default(string[]);

                switch (attribute.ParametersType)
                {
                    case JsonRpcParametersType.ByPosition:
                        {
                            var parameterPositions = attribute.ParameterPositions;

                            if (parameterPositions.Length != parameters.Length)
                            {
                                throw new InvalidOperationException(string.Format(Strings.GetString("service.method.invalid_parameters_count"), method.Name, typeof(T)));
                            }

                            for (var i = 0; i < parameterPositions.Length; i++)
                            {
                                if (!parameterPositions.Contains(i))
                                {
                                    throw new InvalidOperationException(string.Format(Strings.GetString("service.method.invalid_parameter_positions"), method.Name, typeof(T)));
                                }
                            }

                            var parametersContract = new Type[parameters.Length];

                            for (var i = 0; i < parameters.Length; i++)
                            {
                                parametersContract[i] = parameters[i].ParameterType;
                            }

                            contract = new JsonRpcRequestContract(parametersContract);
                        }
                        break;
                    case JsonRpcParametersType.ByName:
                        {
                            var parameterNames = attribute.ParameterNames;

                            if (parameterNames.Length != parameters.Length)
                            {
                                throw new InvalidOperationException(string.Format(Strings.GetString("service.method.invalid_parameters_count"), method.Name, typeof(T)));
                            }
                            if (parameterNames.Length != parameterNames.Distinct(StringComparer.Ordinal).Count())
                            {
                                throw new InvalidOperationException(string.Format(Strings.GetString("service.method.invalid_parameter_names"), method.Name, typeof(T)));
                            }

                            var parametersContract = new Dictionary<string, Type>(parameters.Length, StringComparer.Ordinal);

                            parametersBindings = new string[parameters.Length];

                            for (var i = 0; i < parameters.Length; i++)
                            {
                                parametersContract[parameterNames[i]] = parameters[i].ParameterType;
                                parametersBindings[i] = parameterNames[i];
                            }

                            contract = new JsonRpcRequestContract(parametersContract);
                        }
                        break;
                    default:
                        {
                            if (parameters.Length != 0)
                            {
                                throw new InvalidOperationException(string.Format(Strings.GetString("service.method.invalid_parameters_count"), method.Name, typeof(T)));
                            }

                            contract = new JsonRpcRequestContract();
                        }
                        break;
                }

                contracts[attribute.MethodName] = (contract, method, parameters, parametersBindings);
            }
        }

        IReadOnlyDictionary<string, JsonRpcRequestContract> IJsonRpcHandler.GetContracts()
        {
            return _contracts;
        }

        async Task<JsonRpcResponse> IJsonRpcHandler.HandleAsync(JsonRpcRequest request)
        {
            var requestId = request.Id;
            var (method, parameters, parametersBindings) = _metadata[request.Method];
            var parametersValues = default(object[]);

            switch (request.ParametersType)
            {
                case JsonRpcParametersType.ByPosition:
                    {
                        parametersValues = new object[parameters.Length];

                        for (var i = 0; i < parametersValues.Length; i++)
                        {
                            parametersValues[i] = request.ParametersByPosition[i];
                        }
                    }
                    break;
                case JsonRpcParametersType.ByName:
                    {
                        parametersValues = new object[parameters.Length];

                        for (var i = 0; i < parametersValues.Length; i++)
                        {
                            if (!request.ParametersByName.TryGetValue(parametersBindings[i], out parametersValues[i]))
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

                                    var message = string.Format(Strings.GetString("service.request.parameter.undefined_value"), request.Method, parametersBindings[i]);

                                    return new JsonRpcResponse(new JsonRpcError(JsonRpcErrorCode.InvalidParameters, message), requestId);
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
                    await (dynamic)method.Invoke(_service, parametersValues);
                }
                else
                {
                    return new JsonRpcResponse(await (dynamic)method.Invoke(_service, parametersValues) as object, requestId);
                }
            }
            catch (TargetInvocationException e)
                when (e.InnerException is JsonRpcServiceException jrse)
            {
                return new JsonRpcResponse(new JsonRpcError(jrse.Code, jrse.Message, jrse.ErrorData), requestId);
            }
            catch (TargetInvocationException e)
                when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }

            return null;
        }

        void IDisposable.Dispose()
        {
            (_service as IDisposable)?.Dispose();
        }
    }
}