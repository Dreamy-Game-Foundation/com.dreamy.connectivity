using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public interface IConnectionRecoveryUI
    {
        event Action CancelRequested;
        UniTask<ConnectionRecoveryDecision> ShowAsync(ConnectionRecoveryPrompt prompt, CancellationToken cancellationToken);
        void Close();
    }
}
