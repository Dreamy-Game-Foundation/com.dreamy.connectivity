using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.UI;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Root lifetime for services/model; UI presenter lifetime belongs to PanelManager.</summary>
    public sealed class ConnectivityFeature : IDisposable
    {
        private readonly IConnectionRecoveryUI recoveryUI;
        private readonly ConnectivityLifecycle lifecycle;
        private readonly string targetId;
        private readonly ConnectivityRequirement requirement;
        private readonly CancellationTokenSource lifetime = new();
        private int resumeVersion;
        private bool disposed;
        public ConnectivityService Service { get; }
        public ConnectionRetryModel RetryModel { get; }
        public ConnectivityActionRunner Actions { get; }
        public ConnectivityFeature(IConnectivityGateway gateway, PanelPresenterFactory factory,
            Func<ConnectionRetryModel, IConnectionRecoveryUI> createRecoveryUI, string targetId = "default", ConnectivityRequirement requirement = ConnectivityRequirement.NetworkHintOnly,
            IConnectionSettingsGateway settings = null, ConnectivityLifecycle lifecycle = null)
        {
            if (createRecoveryUI == null) throw new ArgumentNullException(nameof(createRecoveryUI));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Target ID is required.", nameof(targetId));
            this.targetId = targetId; this.requirement = requirement; this.lifecycle = lifecycle;
            Service = new ConnectivityService(gateway);
            settings ??= new ConnectionSettingsGateway();
            RetryModel = new ConnectionRetryModel();
            try
            {
                recoveryUI = createRecoveryUI(RetryModel)
                    ?? throw new InvalidOperationException("The recovery UI factory must return an IConnectionRecoveryUI.");
            }
            catch
            {
                RetryModel.Dispose(); Service.Dispose(); lifetime.Dispose();
                throw;
            }
            factory.Register<ConnectionRetryPanel>(view => new ConnectionRetryPresenter(RetryModel, view, settings.IsSupported, settings.ButtonLabel));
            Actions = new ConnectivityActionRunner(Service, recoveryUI, settings, lifetime: lifetime.Token);
            if (lifecycle != null) { lifecycle.Initialize(Service); lifecycle.Resumed += OnResume; }
        }
        private void OnResume() => RecheckAsync().Forget(Debug.LogException);
        private async UniTask RecheckAsync()
        {
            int version = ++resumeVersion;
            try
            {
                var result = await Service.CheckAsync(new ConnectivityCheckRequest(targetId, requirement, true), lifetime.Token);
                if (version == resumeVersion && !disposed) RetryModel.ReportConnectionAvailability(result.Satisfies(requirement));
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (lifecycle != null) lifecycle.Resumed -= OnResume;
            lifetime.Cancel();
            try
            {
                recoveryUI.Close();
                if (recoveryUI is IDisposable disposable) disposable.Dispose();
            }
            finally { RetryModel.Dispose(); Service.Dispose(); lifetime.Dispose(); }
        }
    }
}
