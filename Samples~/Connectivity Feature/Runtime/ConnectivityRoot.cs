using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Dreamy.UI;
namespace Dreamy.Connectivity
{
    /// <summary>Ready-to-use scene root. Initialize once; dispose the feature when this root is destroyed.</summary>
    [DisallowMultipleComponent]
    public sealed class ConnectivityRoot : MonoBehaviour
    {
        [Tooltip("Addressables key/path for a local Retry panel prefab.")]
        [SerializeField] private string retryPanelAddress = "Panel/ConnectionRetryPanel.prefab";
        [SerializeField] private ConnectivityLifecycle lifecycle;
        [SerializeField] private bool initializeOnStart = true;
        [Tooltip("Check when this scene starts. Offline opens Retry; this does not block other game scripts.")]
        [SerializeField] private bool checkConnectionOnStart;
        [Tooltip("Editor/development builds only. Combine with Check Connection On Start to test the entry Retry panel.")]
        [SerializeField] private bool simulateConnectionLossOnStart;
        [SerializeField] private string targetId = "default";
        [Tooltip("Leave blank for device reachability. Set a host-owned HTTPS health URL to require endpoint health.")]
        [SerializeField] private string healthUrl;
        [SerializeField] private int expectedStatus = 204;
        [SerializeField] private string expectedBody = "";
        private enum SimulationMode { Live, Offline, Connected, Timeout }
        private SimulationMode simulation;
        private CancellationTokenSource simulationAction;
        private CancellationTokenSource startupCheck;
        public ConnectivityFeature Feature { get; private set; }
        public string SimulationStatus { get; private set; } = "Live gateway";
        public bool IsSimulationActionRunning => simulationAction != null || startupCheck != null;
        public bool CanSimulate => Application.isPlaying && (Application.isEditor || Debug.isDebugBuild);

        private sealed class SimulationGateway : IConnectivityGateway
        {
            private readonly ConnectivityRoot root;
            private readonly IConnectivityGateway live;
            public SimulationGateway(ConnectivityRoot root, IConnectivityGateway live)
            { this.root = root; this.live = live; }
            public UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (!root.CanSimulate || root.simulation == SimulationMode.Live)
                    return live.CheckAsync(request, token);
                var status = root.simulation switch
                {
                    SimulationMode.Offline => ConnectivityStatus.NoNetwork,
                    SimulationMode.Timeout => ConnectivityStatus.TimedOut,
                    _ => request.Requirement == ConnectivityRequirement.EndpointRequired
                        ? ConnectivityStatus.EndpointReachable : ConnectivityStatus.NetworkAvailable
                };
                return UniTask.FromResult(new ConnectivityCheckResult(request.TargetId, status));
            }
        }
        private void Start()
        {
            if (initializeOnStart && Feature == null) Initialize();
            if (checkConnectionOnStart)
            {
                if (Feature == null)
                    Debug.LogWarning("Initialize ConnectivityRoot before Start to enable the entry connection check.", this);
                else CheckOnStartAsync().Forget(Debug.LogException);
            }
        }

        private async UniTask CheckOnStartAsync()
        {
            startupCheck = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            try
            {
                // Let this scene's normal Start methods run before opening the local panel.
                // For asynchronous game UI setup, disable this option and call CheckConnectionAsync after setup.
                await UniTask.NextFrame(cancellationToken: startupCheck.Token);
                var result = await CheckConnectionAsync(startupCheck.Token);
                SimulationStatus = "Entry connection check: " + result.Status;
            }
            catch (OperationCanceledException) { SimulationStatus = "Entry connection check cancelled"; }
            finally { startupCheck.Dispose(); startupCheck = null; }
        }
        public void Initialize(IConnectivityGateway gateway = null)
        {
            if (Feature != null) throw new InvalidOperationException("ConnectivityRoot is already initialized.");
            if (CanSimulate && simulateConnectionLossOnStart) SetSimulation(SimulationMode.Offline);
            bool endpoint = !string.IsNullOrWhiteSpace(healthUrl);
            gateway ??= endpoint ? new HttpEndpointConnectivityGateway(targetId, healthUrl, expectedStatus, expectedBody) : new UnityReachabilityGateway();
            if (!PanelManager.HasInstance)
                throw new InvalidOperationException("Initialize ConnectivityRoot after the game's PanelManager UI bootstrap.");
            var panels = PanelManager.Instance;
            var factory = panels.PresenterFactory ?? new PanelPresenterFactory();
            panels.PresenterFactory = factory;
            Feature = ConnectivityFeatureInstaller.Install(factory,
                model => new ConnectionRetryPanelGateway(panels, retryPanelAddress, model),
                lifecycle, new SimulationGateway(this, gateway), targetId,
                endpoint ? ConnectivityRequirement.EndpointRequired : ConnectivityRequirement.NetworkHintOnly);
        }
        public ConnectivityCheckRequest CreateRequest() => new(targetId, string.IsNullOrWhiteSpace(healthUrl)
            ? ConnectivityRequirement.NetworkHintOnly : ConnectivityRequirement.EndpointRequired);
        /// <summary>Probe the gateway without showing UI or executing a game action.</summary>
        public UniTask<ConnectivityCheckResult> CheckConnectivityAsync(CancellationToken token = default)
        {
            EnsureInitialized();
            return Feature.Service.CheckAsync(CreateRequest().Refresh(), token);
        }

        /// <summary>Check with Retry UI; returns success only after connectivity satisfies this root's requirement.</summary>
        public UniTask<ConnectivityActionResult> CheckConnectionAsync(CancellationToken token = default)
            => ExecuteWhenConnectedAsync("connectivity.entry.check",
                cancellation =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    return UniTask.FromResult(ConnectedOperationResult.Success());
                }, new ConnectivityRetryPolicy(ConnectivityRetrySafety.ReadOnly), token);

        /// <summary>Preflight, recover via Retry UI, then run the operation. Default policy never repeats a dispatched mutation.</summary>
        public UniTask<ConnectivityActionResult> ExecuteWhenConnectedAsync(string operationId,
            Func<CancellationToken, UniTask<ConnectedOperationResult>> operation,
            ConnectivityRetryPolicy policy = null, CancellationToken token = default)
        {
            EnsureInitialized();
            return Feature.Actions.ExecuteAsync(operationId, CreateRequest().Refresh(), operation,
                policy ?? new ConnectivityRetryPolicy(), token);
        }

        private void EnsureInitialized()
        {
            if (Feature == null) throw new InvalidOperationException("Initialize ConnectivityRoot before checking or executing network actions.");
        }

        [ContextMenu("Simulation/Lose Connection And Run Test")]
        public void SimulateConnectionLoss()
        {
            if (!CanSimulate) return;
            SetSimulation(SimulationMode.Offline);
            RunSimulationAction();
        }

        [ContextMenu("Simulation/Recover Connection")]
        public void SimulateConnectionRecovery()
        {
            if (!CanSimulate) return;
            SetSimulation(SimulationMode.Connected);
            Feature?.RetryModel.ReportConnectionAvailability(true);
            SimulationStatus = "Simulated connected. Press Retry on the panel to continue.";
        }

        [ContextMenu("Simulation/Timeout And Run Test")]
        public void SimulateConnectionTimeout()
        {
            if (!CanSimulate) return;
            SetSimulation(SimulationMode.Timeout);
            RunSimulationAction();
        }

        [ContextMenu("Simulation/Use Live Gateway")]
        public void UseLiveGateway()
        {
            if (!CanSimulate) return;
            SetSimulation(SimulationMode.Live);
        }

        private void SetSimulation(SimulationMode mode)
        {
            simulation = mode;
            Feature?.Service.Invalidate();
            if (mode != SimulationMode.Live)
                Feature?.RetryModel.ReportConnectionAvailability(mode == SimulationMode.Connected);
            SimulationStatus = "Gateway: " + mode;
        }

        [ContextMenu("Simulation/Run Test Action")]
        public void RunSimulationAction()
        {
            if (!CanSimulate || IsSimulationActionRunning) return;
            if (Feature == null)
            {
                Debug.LogWarning("Initialize ConnectivityRoot before running a simulation action.", this);
                return;
            }
            RunSimulationActionAsync().Forget(Debug.LogException);
        }

        private async UniTask RunSimulationActionAsync()
        {
            simulationAction = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            SimulationStatus = "Test action running...";
            try
            {
                // A safe no-op to exercise the check/recovery UI. No backend operation is sent.
                var result = await Feature.Actions.ExecuteAsync("connectivity.root.simulation", CreateRequest().Refresh(),
                    token => { token.ThrowIfCancellationRequested(); return UniTask.FromResult(ConnectedOperationResult.Success()); },
                    new ConnectivityRetryPolicy(ConnectivityRetrySafety.ReadOnly), simulationAction.Token);
                SimulationStatus = $"Test action: {result.Status}; attempts: {result.Attempts}";
                Debug.Log("[Connectivity Simulation] " + SimulationStatus, this);
            }
            catch (OperationCanceledException) { SimulationStatus = "Test action cancelled"; }
            finally { simulationAction.Dispose(); simulationAction = null; }
        }

        [ContextMenu("Simulation/Cancel Test Action")]
        public void CancelSimulationAction()
        {
            simulationAction?.Cancel();
            startupCheck?.Cancel();
        }

        private void OnDisable() => CancelSimulationAction();
        private void OnDestroy() { CancelSimulationAction(); Feature?.Dispose(); Feature = null; }
    }
}
