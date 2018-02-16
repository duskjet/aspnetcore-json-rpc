using System.Threading.Tasks;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Defines a JSON-RPC 2.0 diagnostic provider.</summary>
    public interface IJsonRpcDiagnosticProvider
    {
        /// <summary>Handles a JSON-RPC 2.0 error code as an asynchronous operation.</summary>
        /// <param name="code">The error code.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task HandleErrorAsync(long code);
    }
}