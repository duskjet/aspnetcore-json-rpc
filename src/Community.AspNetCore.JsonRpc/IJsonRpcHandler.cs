using System.Data.JsonRpc;
using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>A JSON-RPC handler.</summary>
    public interface IJsonRpcHandler
    {
        /// <summary>Creates a scheme for the <see cref="JsonRpcSerializer" />.</summary>
        /// <returns>A <see cref="JsonRpcSerializerScheme" /> instance.</returns>
        JsonRpcSerializerScheme CreateScheme();

        /// <summary>Handles a JSON-RPC notification.</summary>
        /// <param name="request">The JSON-RPC notification request.</param>
        /// <returns>A <see cref="Task" /> instance.</returns>
        Task HandleNotification(JsonRpcRequest request);

        /// <summary>Handles a JSON-RPC regular request.</summary>
        /// <param name="request">The JSON-RPC regular request.</param>
        /// <returns>A <see cref="Task" /> instance.</returns>
        Task<JsonRpcResponse> HandleRequest(JsonRpcRequest request);
    }
}