using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Represents a JSON-RPC handler.</summary>
    public interface IJsonRpcHandler
    {
        /// <summary>Creates a type scheme for the <see cref="JsonRpcSerializer" />.</summary>
        /// <returns>The container with request contracts.</returns>
        IReadOnlyDictionary<string, JsonRpcRequestContract> CreateScheme();

        /// <summary>Handles a JSON-RPC request and create a response if it is required.</summary>
        /// <param name="request">The JSON-RPC request.</param>
        /// <returns>A JSON-RPC response.</returns>
        Task<JsonRpcResponse> Handle(JsonRpcRequest request);
    }
}