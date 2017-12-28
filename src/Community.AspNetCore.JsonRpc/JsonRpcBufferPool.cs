using System.Buffers;
using Newtonsoft.Json;

namespace Community.AspNetCore.JsonRpc
{
    internal sealed class JsonRpcBufferPool : IArrayPool<char>
    {
        private readonly ArrayPool<char> _arrayPool = ArrayPool<char>.Create();

        public char[] Rent(int minimumLength)
        {
            return _arrayPool.Rent(minimumLength);
        }

        public void Return(char[] array)
        {
            _arrayPool.Return(array);
        }
    }
}