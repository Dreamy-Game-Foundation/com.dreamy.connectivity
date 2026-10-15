using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    /// <summary>Wraps SDK reachability; true is a network hint, never backend health.</summary>
    public sealed class DelegateConnectivityGateway : IConnectivityGateway
    {
        private readonly Func<bool> isConnected;
        public DelegateConnectivityGateway(Func<bool> isConnected) => this.isConnected = isConnected ?? throw new ArgumentNullException(nameof(isConnected));
        public UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(new ConnectivityCheckResult(request.TargetId,
                isConnected() ? ConnectivityStatus.NetworkAvailable : ConnectivityStatus.NoNetwork));
        }
    }
}
