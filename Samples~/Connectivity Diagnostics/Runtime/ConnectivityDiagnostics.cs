using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Dreamy.UI;

namespace Dreamy.Connectivity.Diagnostics
{
    /// <summary>Development-only simulation controls. Never installed by Connectivity Feature.</summary>
    public sealed class ConnectivityDiagnostics : MonoBehaviour
    {
        [SerializeField] private string retryPanelAddress = "UI/ConnectionRetryDiagnosticsPanel";
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
        private ConnectivityFeature feature;
        private CancellationTokenSource actionCancellation;
        private bool running;
        private int calls;
        private void OnEnable()
        {
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
        private void Start()
        {
            gateway = new DiagnosticGateway();
            if (!PanelManager.HasInstance)
                throw new InvalidOperationException("Initialize diagnostics after the game's PanelManager UI bootstrap.");
            var panels = PanelManager.Instance;
            var factory = panels.PresenterFactory ?? new PanelPresenterFactory();
            panels.PresenterFactory = factory;
            feature = ConnectivityFeatureInstaller.Install(factory,
                model => new ConnectionRetryPanelGateway(panels, retryPanelAddress, model), lifecycle, gateway);
        }
        private void OnDestroy() { feature?.Dispose(); feature = null; }
        private void SetMode(Mode mode) { gateway.Current = mode; feature.Service.Invalidate(); statusText.text = "Simulated state: " + mode; }
        private void Loss() => SetMode(Mode.Offline);
        private void Recovery() => SetMode(Mode.Connected);
        private void Timeout() => SetMode(Mode.Timeout);
        private void ServerError() => SetMode(Mode.ServerError);
        private void Unknown() => SetMode(Mode.UnknownOutcome);
        private void Cancel() => actionCancellation?.Cancel();
        private void Run() { if (!running) RunAsync().Forget(Debug.LogException); }
        private async UniTask RunAsync()
        {
            running = true; runButton.interactable = false;
            actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            try
            {
                var result = await feature.Actions.ExecuteAsync("diagnostics.read", new ConnectivityCheckRequest("default"), ExecuteOperationAsync,
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
