using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.UI;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Navigation adapter only. PanelManager owns the view and its presenter.</summary>
    public sealed class ConnectionRetryPanelGateway : IConnectionRecoveryUI, IDisposable
    {
        private readonly PanelManager panels;
        private readonly string address;
        private readonly ConnectionRetryModel model;
        private readonly CancellationTokenSource lifetime = new();
        private UniTask closing = UniTask.CompletedTask;
        private bool disposed, opening;
        public event Action CancelRequested
        {
            add => model.CancelRequested += value;
            remove => model.CancelRequested -= value;
        }
        public ConnectionRetryPanelGateway(PanelManager panels, string address, ConnectionRetryModel model)
        {
            this.panels = panels != null ? panels : throw new ArgumentNullException(nameof(panels));
            this.address = !string.IsNullOrWhiteSpace(address) ? address : throw new ArgumentException("Retry panel address is required.", nameof(address));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
        }
        public async UniTask<ConnectionRecoveryDecision> ShowAsync(ConnectionRecoveryPrompt prompt, CancellationToken token)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ConnectionRetryPanelGateway));
            if (opening) throw new InvalidOperationException("Recovery panel already has a pending caller.");
            opening = true;
            using var session = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            try
            {
                await closing;
                session.Token.ThrowIfCancellationRequested();
                // Populate model first: factory-created presenter renders this state during Show.
                var decision = model.WaitForDecisionAsync(prompt, session.Token).AsTask();
                try
                {
                    // Await navigation to completion; never leave a detached late Show after cancellation.
                    await panels.Show<ConnectionRetryPanel>(address);
                    session.Token.ThrowIfCancellationRequested();
                    return await UniTaskExtensions.AsUniTask<ConnectionRecoveryDecision>(decision);
                }
                catch
                {
                    model.Cancel();
                    // Observe the waiter even if the navigation itself failed.
                    try { await decision; } catch (OperationCanceledException) { }
                    throw;
                }
            }
            finally { opening = false; }
        }
        public void Close()
        {
            model.Cancel();
            if (!closing.Status.IsCompleted()) return;
            closing = ClosePanelAsync().Preserve();
            closing.Forget(Debug.LogException);
        }
        private async UniTask ClosePanelAsync()
        {
            if (panels != null) await panels.Close<ConnectionRetryPanel>();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; lifetime.Cancel(); Close(); lifetime.Dispose();
        }
    }
}
