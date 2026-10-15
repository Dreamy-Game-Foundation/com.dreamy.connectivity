using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public readonly struct ConnectivityCheckResult
    {
        public ConnectivityCheckResult(string targetId, ConnectivityStatus status, string errorCode = null, bool fromCache = false)
        { TargetId = targetId; Status = status; ErrorCode = errorCode; FromCache = fromCache; }
        public string TargetId { get; }
        public ConnectivityStatus Status { get; }
        public string ErrorCode { get; }
        public bool FromCache { get; }
        public bool Satisfies(ConnectivityRequirement requirement) =>
            requirement == ConnectivityRequirement.RequestFirst || Status == ConnectivityStatus.EndpointReachable ||
            (requirement == ConnectivityRequirement.NetworkHintOnly && Status == ConnectivityStatus.NetworkAvailable);
        public ConnectivityCheckResult Cached() => new(TargetId, Status, ErrorCode, true);
    }
}
