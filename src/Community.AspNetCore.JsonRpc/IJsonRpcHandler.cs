using System.Collections.Generic;
using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Represents a JSON-RPC 2.0 handler.</summary>
    public interface IJsonRpcHandler
    {
        /// <summary>Creates a scheme for deserializing JSON-RPC requests.</summary>
        /// <returns>A dictionary with JSON-RPC request contracts.</returns>
        IReadOnlyDictionary<string, JsonRpcRequestContract> CreateScheme();

        /// <summary>Handles a JSON-RPC request and returns a JSON-RPC response or <see langword="null" /> for a notification.</summary>
        /// <param name="request">The JSON-RPC request.</param>
        /// <returns>A JSON-RPC response or <see langword="null" />.</returns>
        Task<JsonRpcResponse> Handle(JsonRpcRequest request);
    }
}