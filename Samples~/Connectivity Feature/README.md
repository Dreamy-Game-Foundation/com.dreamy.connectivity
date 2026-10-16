# Connectivity Feature

Production integration sample. Before import, install Dreamy UI 1.0.5, Dreamy Feature 0.2.0 and UGUI 2.0.0, then TMP Essential Resources. These are sample-only prerequisites; the Connectivity package does not depend on them. It contains a root prefab and a Retry panel variant of `BaseFeaturePanel`; it includes optional development simulation controls on the root, using the live gateway by default.

The game scene must have a Canvas PanelManager and one EventSystem configured for its input backend; the root reuses them.

1. Place `Prefabs/ConnectivityRoot.prefab` into the scene.
2. Set Retry Panel Address; the root resolves the game Canvas PanelManager singleton and reuses its shared presenter factory. Its `ConnectivityRoot` initializes on Start. Leave health URL blank for reachability, or supply your team's HTTPS endpoint and expected status/body.
3. Pass your screen cancellation token to `root.Feature.Actions.ExecuteAsync`. Use `root.CreateRequest()` so target/requirement match the configured gateway.
4. To inject the team's SDK: disable Initialize On Start, call `root.Initialize(new DelegateConnectivityGateway(() => YourSdk.Internet_IsConnected()))` from GameInstaller.
5. Replace serialized text/font/colors in a project-owned variant. Forward your UI manager's Back action to `ConnectionRetryPanel.RequestBack`.

Keep exactly one foreground recovery root per game UI scope. Root owns/disposes the service, presenter and lifetime; share it across callers. Do not create a root per button. Use the installer directly if your existing GameInstaller owns the dependencies.

For custom UI, replace `IConnectionRecoveryUI`; for custom backend/SDK, replace `IConnectivityGateway`. See the package README for operation result classification, idempotency and platform limitations. The UI adapter opens the panel with PanelManager.Show; its factory creates a fresh ConnectionRetryPresenter per opening.

The panel, presenter, UI composition and UI tests are owned by this sample. Moving/importing it preserves the supplied `.meta` files; the assembly names and prefab script GUIDs stay stable. Runtime Connectivity only communicates through `IConnectionRecoveryUI`.

MVP: runtime `ConnectionRetryModel` owns recovery state, the sample `ConnectionRetryPresenter` implements `IPanelPresenter`, and `ConnectionRetryPanel` implements `IConnectionRetryView`. Installer registers a presenter constructor with the shared PanelPresenterFactory; it does not instantiate a presenter until PanelManager opens the panel. Root services/model live across opens; presenters are disposed on close.

For direct GameInstaller integration:

```csharp
// The UI root owns navigation and assigns the same sharedFactory used by other features.
var panels = PanelManager.Instance;
panels.PresenterFactory = sharedFactory;
var feature = ConnectivityFeatureInstaller.Install(sharedFactory,
    model => new ConnectionRetryPanelGateway(panels, retryPanelAddress, model),
    lifecycle: lifecycle, gateway: gateway);
// Dispose feature at root teardown; it closes/disposes the returned recovery adapter.
```

Install once per root/factory scope. The installer only registers services/presenters and accepts an `IConnectionRecoveryUI` creation callback; it has no address, manager or singleton dependency. A game can return its own recovery UI implementation. The supplied root holds the address and resolves the manager after PanelManager.Awake (normally Start or after UI bootstrap). Ready-made prefab variants and the diagnostics scene are included; no Editor builder is required.

## Root simulation controls

Select ConnectivityRoot during Play Mode to use its Inspector buttons (or component Simulation context menu): Lose Connection + Run Test, Recover Connection, Simulate Timeout + Run Test, Use Live Gateway, Run Test Action, Cancel Test Action. Loss/timeout run a safe no-op through the action runner to open the Retry panel. Recovery supplies a simulated successful check (including endpoint mode); press Retry to finish the waiting action. Use Live Gateway restores the injected SDK/HTTP/reachability gateway. It does not auto-retry. Repeated Run clicks cannot start overlapping test actions.

Simulation is transient and disabled in release builds. It only starts offline when Simulate Connection Loss On Start is explicitly enabled. The small Editor inspector only draws buttons; it does not build or modify sample assets. No PanelManager changes are required.

## Entry check and action API

Put ConnectivityRoot and the active Canvas PanelManager in MainScene. Enable **Initialize On Start** and **Check Connection On Start**. To test, also enable **Simulate Connection Loss On Start** before Play. On the next frame the root checks connectivity and opens Retry when offline. Recover Connection then Retry continues the check. Cancel/Back returns Cancelled; it does not fail bootstrap or block loading/game scripts. All entry options default off except initialization. The simulation checkbox is ignored in release builds.

For custom asynchronous UI loading, turn off Check Connection On Start and await `root.CheckConnectionAsync(token)` after your UI is ready. Standard scene Start scheduling is not an asynchronous UI readiness signal.

```csharp
// Check only: no panel.
ConnectivityCheckResult probe = await root.CheckConnectivityAsync(token);
bool available = probe.Satisfies(root.CreateRequest().Requirement);

// Check + Retry UI; gate your own continuation on the result.
ConnectivityActionResult check = await root.CheckConnectionAsync(token);
if (!check.IsSuccess) return;

// Prefer this for a real network action: owns both the preflight and actual operation.
ConnectivityActionResult result = await root.ExecuteWhenConnectedAsync(
    "profile.load", LoadProfileAsync,
    new ConnectivityRetryPolicy(ConnectivityRetrySafety.ReadOnly), token);

async UniTask<ConnectedOperationResult> LoadProfileAsync(CancellationToken cancellation)
{
    // Call the project's SDK/backend with cancellation. Map its actual outcome:
    // Success(), ConnectivityFailure, Rejected, ServiceUnavailable or OutcomeUnknown.
    await profileApi.LoadAsync(cancellation); // Project API; replace with your implementation.
    return ConnectedOperationResult.Success();
}
```

Default ExecuteWhenConnectedAsync policy is Never: a dispatched mutation is not blindly repeated. Choose ReadOnly only for safe reads, or IdempotentWithKey with the backend's transaction key. A successful check never guarantees the request succeeds; classify actual transport failures in the callback. A second foreground call during recovery returns Busy. Methods require an initialized root; pass the screen/action owner's cancellation token and handle Cancelled/Failed/RetryExhausted/OutcomeUnknown.
