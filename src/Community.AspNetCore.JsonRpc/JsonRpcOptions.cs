namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Provides JSON-RPC transport options.</summary>
    public sealed class JsonRpcOptions
    {
        /// <summary>Gets or sets maximum size of batch size (1024 is used if not specified).</summary>
        public ushort? MaxBatchSize
        {
            get;
            set;
        }

        /// <summary>Gets or sets maximum length of string message identifier (1024 is used if not specified).</summary>
        public ushort? MaxIdLength
        {
            get;
            set;
        }
    }
}