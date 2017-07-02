using System;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Defines a JSON-RPC method handler.</summary>
    public sealed class JsonRpcMethodAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="JsonRpcMethodAttribute" /> class.</summary>
        /// <param name="name">The name of the defined method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name" /> is <see langword="null" />.</exception>
        public JsonRpcMethodAttribute(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
        }

        /// <summary>Gets a name of the defined method.</summary>
        public string Name { get; }
    }
}