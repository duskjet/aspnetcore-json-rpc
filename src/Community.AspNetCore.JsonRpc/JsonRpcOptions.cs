// © Alexander Kozlenko. Licensed under the MIT License.

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Provides options for the JSON-RPC 2.0 middleware.</summary>
    public sealed class JsonRpcOptions
    {
        /// <summary>Initializes a new instance of the <see cref="JsonRpcOptions" /> class.</summary>
        public JsonRpcOptions()
        {
        }

        /// <summary>Gets or sets the maximum size of batch size.</summary>
        public ushort? MaxBatchSize
        {
            get;
            set;
        }

        /// <summary>Gets or sets the maximum length of string message identifier.</summary>
        public ushort? MaxIdLength
        {
            get;
            set;
        }
    }
}