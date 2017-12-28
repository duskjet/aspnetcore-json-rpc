using System;

namespace Community.AspNetCore.JsonRpc
{
    /// <summary>
    /// A JSON-RPC service exception.
    /// </summary>
    public sealed class JsonRpcServiceException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="JsonRpcServiceException" /> class.</summary>
        /// <param name="code">The number that indicates the error type that occurred.</param>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="data">The value that contains additional information about the error.</param>
        public JsonRpcServiceException(long code, string message, object data = null)
            : base(message)
        {
            Code = code;
            RpcData = data;
        }

        /// <summary>Gets a number that indicates the error type that occurred.</summary>
        public long Code
        {
            get;
        }

        /// <summary>Gets an optional value that contains additional information about the error.</summary>
        public object RpcData
        {
            get;
        }
    }
}