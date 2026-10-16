using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Connectivity.Diagnostics
{
    /// <summary>Development-only simulation controls. Never installed by Connectivity Feature.</summary>
    public sealed class ConnectivityDiagnostics : MonoBehaviour
    {
        [SerializeField] private ConnectionRetryPanel retryPanel;
        [SerializeField] private ConnectivityLifecycle lifecycle;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button runButton;
        [SerializeField] private Button lossButton;
        [SerializeField] private Button recoveryButton;
        [SerializeField] private Button timeoutButton;
        [SerializeField] private Button serverErrorButton;
        [SerializeField] private Button unknownOutcomeButton;
        [SerializeField] private Button cancelButton;
        private enum Mode { Connected, Offline, Timeout, ServerError, UnknownOutcome }
        private sealed class DiagnosticGateway : IConnectivityGateway
        {
            public Mode Current;
            public async UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken token)
            {
                if (Current == Mode.Timeout) await UniTask.Delay(TimeSpan.FromSeconds(30), ignoreTimeScale: true, cancellationToken: token);
                token.ThrowIfCancellationRequested();
                return new ConnectivityCheckResult(request.TargetId, Current == Mode.Offline ? ConnectivityStatus.NoNetwork : ConnectivityStatus.NetworkAvailable);
            }
        }
        private DiagnosticGateway gateway;
        private ConnectivityService service;
        private ConnectionRetryModel retryModel;
        private SceneRecoveryUI recoveryUI;
        private ConnectivityActionRunner actions;
        private readonly CancellationTokenSource lifetime = new();
        private int resumeVersion;

        // The diagnostics scene owns visibility and presenter lifetime directly.
        // Do not call UIPanel.Init/Show/Hide: those are PanelManager navigation APIs.
        private sealed class SceneRecoveryUI : IConnectionRecoveryUI, IDisposable
        {
            private readonly ConnectionRetryPanel view;
            private readonly ConnectionRetryModel model;
            private readonly ConnectionRetryPresenter presenter;
            public event Action CancelRequested
            {
                add => model.CancelRequested += value;
                remove => model.CancelRequested -= value;
            }
            public SceneRecoveryUI(ConnectionRetryPanel view, ConnectionRetryModel model, IConnectionSettingsGateway settings)
            {
                this.view = view;
                this.model = model;
                view.gameObject.SetActive(false);
                presenter = new ConnectionRetryPresenter(model, view, settings.IsSupported, settings.ButtonLabel);
            }
            public async UniTask<ConnectionRecoveryDecision> ShowAsync(ConnectionRecoveryPrompt prompt, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var decision = model.WaitForDecisionAsync(prompt, token);
                view.gameObject.SetActive(true);
                presenter.Show();
                return await decision;
            }
            public void Close()
            {
                model.Cancel();
                if (view != null) view.gameObject.SetActive(false);
            }
            public void Dispose() { Close(); presenter.Dispose(); }
        }
        private CancellationTokenSource actionCancellation;
        private bool running;
        private int calls;
        private void OnEnable()
        {
            runButton.interactable = !running;
            runButton.onClick.AddListener(Run); lossButton.onClick.AddListener(Loss); recoveryButton.onClick.AddListener(Recovery);
            timeoutButton.onClick.AddListener(Timeout); serverErrorButton.onClick.AddListener(ServerError);
            unknownOutcomeButton.onClick.AddListener(Unknown); cancelButton.onClick.AddListener(Cancel);
            statusText.text = "Diagnostics only. Choose a connection state, then Run Action.";
        }
        private void OnDisable()
        {
            runButton.onClick.RemoveListener(Run); lossButton.onClick.RemoveListener(Loss); recoveryButton.onClick.RemoveListener(Recovery);
            timeoutButton.onClick.RemoveListener(Timeout); serverErrorButton.onClick.RemoveListener(ServerError);
            unknownOutcomeButton.onClick.RemoveListener(Unknown); cancelButton.onClick.RemoveListener(Cancel);
            actionCancellation?.Cancel();
        }
        private void Awake()
        {
            if (retryPanel == null) throw new InvalidOperationException("Assign the scene-owned Retry panel.");
            gateway = new DiagnosticGateway();
            service = new ConnectivityService(gateway);
            retryModel = new ConnectionRetryModel();
            var settings = new ConnectionSettingsGateway();
            recoveryUI = new SceneRecoveryUI(retryPanel, retryModel, settings);
            actions = new ConnectivityActionRunner(service, recoveryUI, settings, lifetime: lifetime.Token);
            if (lifecycle != null) { lifecycle.Initialize(service); lifecycle.Resumed += OnResume; }
        }
        private void OnResume() => RecheckAfterResumeAsync().Forget(Debug.LogException);
        private async UniTask RecheckAfterResumeAsync()
        {
            int version = ++resumeVersion;
            try
            {
                var result = await service.CheckAsync(new ConnectivityCheckRequest("default", forceRefresh: true), lifetime.Token);
                if (version == resumeVersion && !lifetime.IsCancellationRequested)
                    retryModel.ReportConnectionAvailability(result.Satisfies(ConnectivityRequirement.NetworkHintOnly));
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
        private void OnDestroy()
        {
            if (lifecycle != null) lifecycle.Resumed -= OnResume;
            lifetime.Cancel();
            recoveryUI?.Dispose();
            retryModel?.Dispose();
            service?.Dispose();
            lifetime.Dispose();
        }
        private void SetMode(Mode mode)
        {
            gateway.Current = mode;
            service.Invalidate();
            retryModel.ReportConnectionAvailability(mode != Mode.Offline && mode != Mode.Timeout);
            statusText.text = "Simulated state: " + mode + (running ? ". Press Retry to continue." : "");
        }
        private void Loss() => SetMode(Mode.Offline);
        private void Recovery() => SetMode(Mode.Connected);
        private void Timeout() => SetMode(Mode.Timeout);
        private void ServerError() => SetMode(Mode.ServerError);
        private void Unknown() => SetMode(Mode.UnknownOutcome);
        private void Cancel() => actionCancellation?.Cancel();
        private void Run() { if (isActiveAndEnabled && !running) RunAsync().Forget(Debug.LogException); }
        private async UniTask RunAsync()
        {
            running = true; runButton.interactable = false;
            statusText.text = "Running connection check / action...";
            actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            try
            {
                var result = await actions.ExecuteAsync("diagnostics.read", new ConnectivityCheckRequest("default", forceRefresh: true), ExecuteOperationAsync,
                    new ConnectivityRetryPolicy(ConnectivityRetrySafety.ReadOnly), actionCancellation.Token);
                if (this != null && isActiveAndEnabled) statusText.text = $"Result: {result.Status}; attempts: {result.Attempts}; total calls: {calls}; {result.ErrorCode}";
            }
            finally
            {
                actionCancellation.Dispose(); actionCancellation = null; running = false;
                if (this != null && isActiveAndEnabled) runButton.interactable = true;
            }
        }
        private async UniTask<ConnectedOperationResult> ExecuteOperationAsync(CancellationToken token)
        {
            calls++;
            await UniTask.Delay(TimeSpan.FromMilliseconds(300), ignoreTimeScale: true, cancellationToken: token);
            return gateway.Current switch
            {
                Mode.Offline => new ConnectedOperationResult(ConnectedOperationStatus.ConnectivityFailure, "simulated_transport_loss"),
                Mode.ServerError => new ConnectedOperationResult(ConnectedOperationStatus.ServiceUnavailable, "simulated_503"),
                Mode.UnknownOutcome => new ConnectedOperationResult(ConnectedOperationStatus.OutcomeUnknown, "reconcile_transaction_before_retry"),
                _ => ConnectedOperationResult.Success()
            };
        }
    }
}
