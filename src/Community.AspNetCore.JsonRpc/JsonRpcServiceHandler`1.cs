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
        private readonly IDictionary<string, MethodInfo> _methodMap = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        private readonly IDictionary<string, string[]> _parameterMaps = new Dictionary<string, string[]>(StringComparer.Ordinal);
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
        }

        public JsonRpcSerializerScheme CreateScheme()
        {
            _methodMap.Clear();
            _parameterMaps.Clear();

            var scheme = new JsonRpcSerializerScheme();

            foreach (var methodInfo in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                var methodNameAttribute = methodInfo.GetCustomAttribute<JsonRpcNameAttribute>();

                if (methodNameAttribute == null)
                {
                    continue;
                }
                if (!(methodInfo.ReturnType == typeof(Task)) && !(methodInfo.ReturnType.IsGenericType && (methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))))
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.invalid_type"), methodNameAttribute.Value));
                }
                if (_methodMap.ContainsKey(methodNameAttribute.Value))
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.duplicate_name"), methodNameAttribute.Value));
                }

                _methodMap[methodNameAttribute.Value] = methodInfo;

                var methodScheme = default(JsonRpcMethodScheme);
                var methodParameters = methodInfo.GetParameters();

                if (methodParameters.Length == 0)
                {
                    methodScheme = new JsonRpcMethodScheme();
                }
                else
                {
                    if (methodParameters[0].GetCustomAttribute<JsonRpcNameAttribute>() == null)
                    {
                        var methodParameterTypes = new Type[methodParameters.Length];

                        for (var i = 0; i < methodParameters.Length; i++)
                        {
                            if (methodParameters[i].GetCustomAttribute<JsonRpcNameAttribute>() != null)
                            {
                                throw new InvalidOperationException(
                                    string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.parameter.name.unexpected"), methodParameters[i].Name));
                            }

                            methodParameterTypes[i] = methodParameters[i].ParameterType;
                        }

                        methodScheme = new JsonRpcMethodScheme(methodParameterTypes);
                    }
                    else
                    {
                        var methodParameterTypes = new Dictionary<string, Type>(methodParameters.Length, StringComparer.Ordinal);

                        _parameterMaps[methodNameAttribute.Value] = new string[methodParameters.Length];

                        for (var i = 0; i < methodParameters.Length; i++)
                        {
                            var parameterNameAttribute = methodParameters[i].GetCustomAttribute<JsonRpcNameAttribute>();

                            if (parameterNameAttribute == null)
                            {
                                throw new InvalidOperationException(
                                    string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.parameter.name.undefined"), methodParameters[i].Name));
                            }
                            if (methodParameterTypes.ContainsKey(parameterNameAttribute.Value))
                            {
                                throw new InvalidOperationException(
                                    string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.method.parameter.name.duplicate_name"), methodNameAttribute.Value, parameterNameAttribute.Value));

                            }

                            methodParameterTypes[parameterNameAttribute.Value] = methodParameters[i].ParameterType;

                            _parameterMaps[methodNameAttribute.Value][i] = parameterNameAttribute.Value;
                        }

                        methodScheme = new JsonRpcMethodScheme(methodParameterTypes);
                    }
                }

                scheme.Methods[methodNameAttribute.Value] = methodScheme;
            }

            return scheme;
        }

        public async Task<JsonRpcResponse> Handle(JsonRpcRequest request)
        {
            var methodInfo = _methodMap[request.Method];
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
                        var parameterMap = _parameterMaps[request.Method];

                        parameters = new object[parameterMap.Length];

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            if (!request.ParamsByName.TryGetValue(parameterMap[i], out var parameterValue))
                            {
                                var error = new JsonRpcError(-32602L,
                                    string.Format(CultureInfo.InvariantCulture, Strings.GetString("service.request.parameter.undefined"), parameterMap[i]));

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

            if (request.IsNotification || !methodInfo.ReturnType.IsGenericType)
            {
                try
                {
                    await ((Task)methodInfo.Invoke(_service, parameters)).ConfigureAwait(false);
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
                    var result = await ((dynamic)methodInfo.Invoke(_service, parameters)).ConfigureAwait(false) as object;

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