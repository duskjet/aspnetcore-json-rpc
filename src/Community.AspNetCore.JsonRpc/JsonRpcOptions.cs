using Microsoft.AspNetCore.Http;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Provides JSON-RPC transport options.</summary>
    public sealed class JsonRpcOptions
    {
        /// <summary>Initializes a new instance of the <see cref="JsonRpcOptions" /> class.</summary>
        public JsonRpcOptions()
        {
        }

        /// <summary>Gets an identifier to store JSON-RPC error codes in shared request data of the particular <see cref="HttpContext" />.</summary>
        public static string HttpContextErrorIdentifier
        {
            get => "JSON_RPC_ERROR_CODE";
        }

        /// <summary>Gets or sets the maximum size of batch size (1024 is used if not specified).</summary>
        public ushort? MaxBatchSize
        {
            get;
            set;
        }

        /// <summary>Gets or sets the maximum length of string message identifier (1024 is used if not specified).</summary>
        public ushort? MaxIdLength
        {
            get;
            set;
        }
    }
}