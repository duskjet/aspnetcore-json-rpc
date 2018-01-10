using System;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Defines a JSON-RPC named item.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public sealed class JsonRpcNameAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="JsonRpcNameAttribute" /> class.</summary>
        /// <param name="name">The name of the item.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null" />.</exception>
        public JsonRpcNameAttribute(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Value = name;
        }

        /// <summary>Gets a name of the item.</summary>
        public string Value
        {
            get;
        }
    }
}