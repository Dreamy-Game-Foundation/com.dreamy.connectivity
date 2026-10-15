using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Production presenter; no SDK or wallet access in the view.</summary>
    public sealed class ConnectionRecoveryPresenter : IConnectionRecoveryUI, IDisposable
    {
        public event Action CancelRequested;
        private readonly IConnectionRecoveryView view;
        private readonly IConnectionSettingsGateway settings;
        private UniTaskCompletionSource<ConnectionRecoveryDecision> pending;
        private bool disposed;
        public ConnectionRecoveryPresenter(IConnectionRecoveryView view, IConnectionSettingsGateway settings)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view)); this.settings = settings;
            view.DecisionRequested += Decide; view.Closed += Closed;
        }
        public async UniTask<ConnectionRecoveryDecision> ShowAsync(ConnectionRecoveryPrompt prompt, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ConnectionRecoveryPresenter));
            cancellationToken.ThrowIfCancellationRequested();
            if (pending != null) throw new InvalidOperationException("Recovery prompt already has an active waiter.");
            var waiter = new UniTaskCompletionSource<ConnectionRecoveryDecision>(); pending = waiter;
            try
            {
                view.Render(prompt, settings?.IsSupported == true, settings?.ButtonLabel);
                view.SetBusy(false); view.SetVisible(true);
                return await waiter.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                if (ReferenceEquals(pending, waiter)) pending = null;
                if (!disposed) view.SetBusy(true);
            }
        }
        private void Decide(ConnectionRecoveryDecision decision)
        {
            if (decision == ConnectionRecoveryDecision.Cancel) CancelRequested?.Invoke();
            pending?.TrySetResult(decision);
        }
        private void Closed() { CancelRequested?.Invoke(); pending?.TrySetResult(ConnectionRecoveryDecision.Cancel); }
        public void Close()
        {
            pending?.TrySetResult(ConnectionRecoveryDecision.Cancel);
            if (!disposed) view.SetVisible(false);
        }
        public void Dispose()
        {
            if (disposed) return;
            Close(); disposed = true;
            view.DecisionRequested -= Decide; view.Closed -= Closed;
        }
    }
}
