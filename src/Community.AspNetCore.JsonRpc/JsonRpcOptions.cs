namespace Community.AspNetCore.JsonRpc
{
    /// <summary>Provides JSON-RPC transport options.</summary>
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