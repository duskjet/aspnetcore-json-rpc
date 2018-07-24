// © Alexander Kozlenko. Licensed under the MIT License.

using System;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Defines a JSON-RPC metadata item.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public sealed class JsonRpcNameAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="JsonRpcNameAttribute" /> class.</summary>
        /// <param name="name">The name of the metadata item.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null" />.</exception>
        public JsonRpcNameAttribute(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Value = name;
        }

        /// <summary>Gets a name of the metadata item.</summary>
        public string Value
        {
            get;
        }
    }
}