namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Defines JSON-RPC transport constants.</summary>
    public static class JsonRpcTransportConstants
    {
        /// <summary>The JSON-RPC 2.0 MIME type.</summary>
        public const string MimeType = "application/json";

        /// <summary>The identifier used to store JSON-RPC error codes in the shared request data.</summary>
        public const string ScopeErrorsIdentifier = "JSON_RPC.ERROR_CODES";
    }
}