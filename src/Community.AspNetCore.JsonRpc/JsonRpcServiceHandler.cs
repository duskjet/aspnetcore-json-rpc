using System;
using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcServiceHandler : IJsonRpcHandler
    {
        private static readonly MethodInfo _handleResponseMethodInfo = typeof(JsonRpcServiceHandler).GetTypeInfo().GetDeclaredMethod(nameof(HandleResponseTask));
        private static readonly object[] _emptyMethodParameters = new object[] { };

        private readonly IDictionary<string, Tuple<MethodInfo, Type, Type>> _definitions = new Dictionary<string, Tuple<MethodInfo, Type, Type>>(StringComparer.Ordinal);
        private readonly IJsonRpcService _service;

        public JsonRpcServiceHandler(IJsonRpcService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException();
            }

            _service = service;
        }

        public JsonRpcSerializerScheme CreateScheme()
        {
            var scheme = new JsonRpcSerializerScheme();

            foreach (var method in _service.GetType().GetTypeInfo().DeclaredMethods)
            {
                if (method.IsStatic || !method.IsPublic)
                {
                    continue;
                }

                var jsonRpcMethodAttribute = method.GetCustomAttribute<JsonRpcMethodAttribute>();

                if (jsonRpcMethodAttribute == null)
                {
                    continue;
                }

                if (_definitions.ContainsKey(jsonRpcMethodAttribute.Name))
                {
                    throw new InvalidOperationException($"JSON-RPC method \"{jsonRpcMethodAttribute.Name}\" must be unique");
                }

                var parameters = method.GetParameters();

                if (parameters.Length > 1)
                {
                    throw new InvalidOperationException($"JSON-RPC method \"{jsonRpcMethodAttribute.Name}\" must have zero or one parameter");
                }

                var parameterType = parameters.Length == 1 ? parameters[0].ParameterType : null;

                if (method.ReturnType == typeof(Task))
                {
                    _definitions[jsonRpcMethodAttribute.Name] = new Tuple<MethodInfo, Type, Type>(method, parameterType, default(Type));

                    scheme.Methods[jsonRpcMethodAttribute.Name] = new JsonRpcMethodScheme(true, parameterType);
                }
                else
                {
                    var resultType = default(Type);
                    var returnTypeInfo = method.ReturnType.GetTypeInfo();

                    if (returnTypeInfo.IsGenericType)
                    {
                        var genericReturnType = returnTypeInfo.GetGenericTypeDefinition();

                        if (genericReturnType == typeof(Task<>))
                        {
                            resultType = method.ReturnType.GenericTypeArguments[0];
                        }
                    }

                    if (resultType != null)
                    {
                        _definitions[jsonRpcMethodAttribute.Name] = new Tuple<MethodInfo, Type, Type>(method, parameterType, resultType);

                        scheme.Methods[jsonRpcMethodAttribute.Name] = new JsonRpcMethodScheme(false, parameterType);
                    }
                    else
                    {
                        throw new InvalidOperationException($"JSON-RPC method \"{jsonRpcMethodAttribute.Name}\" must have return type of \"System.Threading.Tasks.Task\" or \"System.Threading.Tasks.Task`1\"");
                    }
                }
            }

            return scheme;
        }

        public async Task HandleNotification(JsonRpcRequest request)
        {
            var method = _definitions[request.Method];
            var methodParameters = method.Item2 != null ? new[] { request.HasParams ? request.Params : GetDefaultValue(method.Item2) } : _emptyMethodParameters;

            try
            {
                var methodTask = (Task)method.Item1.Invoke(_service, methodParameters);

                await methodTask.ConfigureAwait(false);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is JsonRpcException jrb)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();

                throw new InvalidOperationException();
            }
        }

        public async Task<JsonRpcResponse> HandleRequest(JsonRpcRequest request)
        {
            var method = _definitions[request.Method];
            var methodParameters = method.Item2 != null ? new[] { request.HasParams ? request.Params : GetDefaultValue(method.Item2) } : _emptyMethodParameters;

            try
            {
                var methodTask = (Task)method.Item1.Invoke(_service, methodParameters);
                var handler = _handleResponseMethodInfo.MakeGenericMethod(method.Item3);
                var handlerParameters = new object[] { request.Id, methodTask };
                var responseTask = (Task<JsonRpcResponse>)handler.Invoke(this, handlerParameters);

                return await responseTask.ConfigureAwait(false);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is JsonRpcException jrb)
            {
                if (jrb is JsonRpcServiceException jrs)
                {
                    return new JsonRpcResponse(new JsonRpcError(jrs.Code, jrs.Message, jrs.JsonRpcData), request.Id);
                }
                else
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();

                    throw new InvalidOperationException();
                }
            }
        }

        private async Task<JsonRpcResponse> HandleResponseTask<T>(JsonRpcId id, Task<T> task)
        {
            return new JsonRpcResponse(await task.ConfigureAwait(false), id);
        }

        private static object GetDefaultValue(Type type)
        {
            return type.GetTypeInfo().IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}