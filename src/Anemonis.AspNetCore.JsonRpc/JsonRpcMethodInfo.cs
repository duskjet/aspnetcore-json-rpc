// © Alexander Kozlenko. Licensed under the MIT License.

using System.Reflection;

namespace Anemonis.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcMethodInfo
    {
        public JsonRpcMethodInfo(MethodInfo method, ParameterInfo[] parameters)
        {
            Method = method;
            Parameters = parameters;
        }

        public JsonRpcMethodInfo(MethodInfo method, ParameterInfo[] parameters, int[] parameterPositions)
            : this(method, parameters)
        {
            ParameterPositions = parameterPositions;
        }

        public JsonRpcMethodInfo(MethodInfo method, ParameterInfo[] parameters, string[] parameterNames)
            : this(method, parameters)
        {
            ParameterNames = parameterNames;
        }

        public MethodInfo Method
        {
            get;
        }

        public ParameterInfo[] Parameters
        {
            get;
        }

        public int[] ParameterPositions
        {
            get;
        }

        public string[] ParameterNames
        {
            get;
        }
    }
}