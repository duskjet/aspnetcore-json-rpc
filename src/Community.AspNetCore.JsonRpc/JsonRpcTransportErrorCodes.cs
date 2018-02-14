namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Defines JSON-RPC transport error codes.</summary>
    public static class JsonRpcTransportErrorCodes
    {
        /// <summary>The error code which specifies, that the provided batch contains requests with duplicate identifiers.</summary>
        public const long DuplicateIdentifiers = -32000L;

        /// <summary>The error code which specifies, that the provided message identifier exceeds length limit.</summary>
        public const long InvalidIdLength = -32010L;

        /// <summary>The error code which specifies, that the provided batch exceeds size limit.</summary>
        public const long InvalidBatchSize = -32020L;
    }
}