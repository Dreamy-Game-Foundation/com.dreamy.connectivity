using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public interface IConnectivityService
    {
        UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken cancellationToken);
        void Invalidate();
    }
}
