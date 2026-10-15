using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Owned by the game root. Use one instance for foreground recovery.</summary>
    public sealed class ConnectivityFeature : IDisposable
    {
        private readonly ConnectionRecoveryPresenter presenter;
        private readonly ConnectivityLifecycle lifecycle;
        private readonly ConnectionRetryPanel view;
        private readonly string targetId;
        private readonly ConnectivityRequirement requirement;
        private readonly CancellationTokenSource lifetime = new();
        private int resumeVersion;
        private bool disposed;
        public ConnectivityService Service { get; }
        public ConnectivityActionRunner Actions { get; }
        public ConnectivityFeature(IConnectivityGateway gateway, ConnectionRetryPanel view,
            string targetId = "default", ConnectivityRequirement requirement = ConnectivityRequirement.NetworkHintOnly,
            IConnectionSettingsGateway settings = null, ConnectivityLifecycle lifecycle = null)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Target ID is required.", nameof(targetId));
            this.view = view; this.targetId = targetId; this.requirement = requirement; this.lifecycle = lifecycle;
            Service = new ConnectivityService(gateway);
            settings ??= new ConnectionSettingsGateway();
            presenter = new ConnectionRecoveryPresenter(view, settings);
            Actions = new ConnectivityActionRunner(Service, presenter, settings, lifetime: lifetime.Token);
            view.SetVisible(false);
            if (lifecycle != null) { lifecycle.Initialize(Service); lifecycle.Resumed += OnResume; }
        }
        private void OnResume() => RecheckAsync().Forget(Debug.LogException);
        private async UniTask RecheckAsync()
        {
            int version = ++resumeVersion;
            try
            {
                var result = await Service.CheckAsync(new ConnectivityCheckRequest(targetId, requirement, true), lifetime.Token);
                if (version == resumeVersion && view != null && view.isActiveAndEnabled)
                    view.SetStatus(result.Satisfies(requirement) ? "Connection available. Press Retry to continue." : "Connection is still unavailable. Check settings and retry.");
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (lifecycle != null) lifecycle.Resumed -= OnResume;
            lifetime.Cancel(); presenter.Dispose(); Service.Dispose(); lifetime.Dispose();
        }
    }
}
