using System;
using System.Threading;
using Cysharp.Threading.Tasks;
namespace Dreamy.Connectivity
{
    /// <summary>Engine-independent recovery state. Does not open, own or reference a panel.</summary>
    public sealed class ConnectionRetryModel : IDisposable
    {
        private UniTaskCompletionSource<ConnectionRecoveryDecision> pending;
        private ConnectionRecoveryPrompt prompt;
        private bool? connectionAvailable;
        private bool disposed;
        public event Action StateChanged;
        public event Action CancelRequested;
        public ConnectionRetryState State => new(prompt, pending != null, connectionAvailable);
        public async UniTask<ConnectionRecoveryDecision> WaitForDecisionAsync(ConnectionRecoveryPrompt prompt, CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ConnectionRetryModel));
            token.ThrowIfCancellationRequested();
            if (pending != null) throw new InvalidOperationException("A recovery decision is already pending.");
            this.prompt = prompt; connectionAvailable = null;
            var waiter = new UniTaskCompletionSource<ConnectionRecoveryDecision>(); pending = waiter;
            try { StateChanged?.Invoke(); return await waiter.Task.AttachExternalCancellation(token); }
            finally
            {
                if (ReferenceEquals(pending, waiter)) pending = null;
                if (!disposed) StateChanged?.Invoke();
            }
        }
        public void Decide(ConnectionRecoveryDecision decision)
        {
            if (disposed) return;
            if (decision == ConnectionRecoveryDecision.Cancel) { Cancel(); return; }
            if (pending == null || (decision == ConnectionRecoveryDecision.Retry && !prompt.CanRetry)) return;
            if (decision != ConnectionRecoveryDecision.Retry && decision != ConnectionRecoveryDecision.OpenSettings) return;
            pending.TrySetResult(decision);
        }
        public void ReportConnectionAvailability(bool available)
        {
            if (disposed) return;
            connectionAvailable = available; StateChanged?.Invoke();
        }
        public void Cancel()
        {
            if (disposed) return;
            // Event first: cancels the action even when it is checking and has no decision waiter.
            CancelRequested?.Invoke();
            pending?.TrySetResult(ConnectionRecoveryDecision.Cancel);
        }
        public void Dispose()
        {
            if (disposed) return;
            Cancel(); disposed = true;
            StateChanged = null; CancelRequested = null;
        }
    }
}
