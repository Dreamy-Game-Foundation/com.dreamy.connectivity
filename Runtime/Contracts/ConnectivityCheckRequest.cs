using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public readonly struct ConnectivityCheckRequest
    {
        public ConnectivityCheckRequest(string targetId, ConnectivityRequirement requirement = ConnectivityRequirement.NetworkHintOnly, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Target ID is required.", nameof(targetId));
            if (!Enum.IsDefined(typeof(ConnectivityRequirement), requirement)) throw new ArgumentOutOfRangeException(nameof(requirement));
            TargetId = targetId; Requirement = requirement; ForceRefresh = forceRefresh;
        }
        public string TargetId { get; }
        public ConnectivityRequirement Requirement { get; }
        public bool ForceRefresh { get; }
        public ConnectivityCheckRequest Refresh() => new(TargetId, Requirement, true);
    }
}
