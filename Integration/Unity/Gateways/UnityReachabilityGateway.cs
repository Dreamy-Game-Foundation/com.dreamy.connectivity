using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    public sealed class UnityReachabilityGateway : IConnectivityGateway
    {
        private readonly DelegateConnectivityGateway gateway = new(() => Application.internetReachability != NetworkReachability.NotReachable);
        public UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken cancellationToken) => gateway.CheckAsync(request, cancellationToken);
    }
}
