using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public enum ConnectivityRetrySafety { Never, ReadOnly, IdempotentWithKey }
    public sealed class ConnectivityRetryPolicy
    {
        public ConnectivityRetryPolicy(ConnectivityRetrySafety safety = ConnectivityRetrySafety.Never,
            int maxRetries = 3, int autoRetries = 0, string idempotencyKey = null)
        {
            if (!Enum.IsDefined(typeof(ConnectivityRetrySafety), safety)) throw new ArgumentOutOfRangeException(nameof(safety));
            if (maxRetries < 0 || autoRetries < 0 || autoRetries > maxRetries) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            if (safety == ConnectivityRetrySafety.IdempotentWithKey && string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("Idempotent retries require the host transaction key.", nameof(idempotencyKey));
            if (safety == ConnectivityRetrySafety.Never && autoRetries != 0) throw new ArgumentException("Unsafe operations cannot auto retry.");
            Safety = safety; MaxRetries = maxRetries; AutoRetries = autoRetries; IdempotencyKey = idempotencyKey;
        }
        public ConnectivityRetrySafety Safety { get; }
        public int MaxRetries { get; }
        public int AutoRetries { get; }
        /// <summary>The host callback must use this same key and immutable payload on its backend.</summary>
        public string IdempotencyKey { get; }
    }
}
