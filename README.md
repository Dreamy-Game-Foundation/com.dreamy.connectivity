# Dreamy Connectivity

Reusable connectivity checks, foreground action recovery and Android/iOS settings.
Version `0.1.0`, Unity 6000.0+. Domain is engine independent; gateways and UI are replaceable.

## Install and import

Install `package.json` from disk, or use a pinned commit from `https://github.com/Dreamy-Game-Foundation/com.dreamy.connectivity.git`. This sandbox uses the Git submodule at `LocalPackages/com.dreamy.connectivity` through `file:../LocalPackages/com.dreamy.connectivity`. Local source tag `v0.1.0` identifies this reviewed baseline; native/device gates remain in VALIDATION.md.

Package dependencies: UniTask 2.5.10+, UnityWebRequest and AndroidJNI modules. **Installing Connectivity does not install Dreamy UI, Dreamy Feature or UGUI.** All panel code, its presenter/composition and UI tests are shipped under `Samples~`, so they are not compiled unless imported.

Before importing **Connectivity Feature**, install these optional sample prerequisites yourself:

- `com.dreamy.ui` 1.0.5 and its dependencies.
- `com.dreamy.feature` 0.2.0, which contains `BaseFeaturePanel.prefab`.
- `com.unity.ugui` 2.0.0, including TMP support.

Import TMP Essential Resources, then Package Manager → Dreamy Connectivity → **Connectivity Feature**. This is the production integration with optional development simulation buttons on ConnectivityRoot. The live gateway remains the default; simulation never runs on startup and is disabled in release builds. Prefabs use the standard LiberationSans SDF from TMP Essential Resources; replace fonts/art/text for your game.

- `Prefabs/ConnectivityRoot.prefab`: scene root, lifecycle bridge and Retry prefab reference (no pre-instantiated panel), using device reachability by default.
- `Prefabs/ConnectionRetryPanel.prefab`: complete Canvas modal, safe area, buttons, text and serialized references.
- `ConnectivityRoot`: initializes on Start by default. For your own GameInstaller, disable `Initialize On Start` and call `Initialize` at your chosen Ready stage.
- `ConnectivityFeatureInstaller`: composition API for the shared presenter factory and injected recovery UI.

`ConnectionRetryPanel` inherits `UIPanel` and is a variant of `BaseFeaturePanel`. The sample opens it exclusively through `PanelManager.Show<ConnectionRetryPanel>(address)`. The manager creates a `ConnectionRetryPresenter` through `PanelPresenterFactory` and disposes it on hide/disable/destroy. The view never opens itself or creates a presenter.

## MVP ownership

| Component | Owner | Responsibility |
| --- | --- | --- |
| `ConnectivityService` / `ConnectivityActionRunner` | Runtime package | Checks, cache, retries and action safety |
| `ConnectionRetryModel` / `ConnectionRetryState` | Runtime package | Recovery state and one pending Retry/Settings/Cancel decision; no UI reference |
| `ConnectionRetryPresenter : IPanelPresenter` | Feature sample | Bind model state and view intent for one panel opening |
| `IConnectionRetryView` / `ConnectionRetryPanel` | Feature sample | Render state and emit button intent |
| `ConnectionRetryPanelGateway : IConnectionRecoveryUI` | Feature sample | Open/close through PanelManager and await the model decision |
| `ConnectivityFeatureInstaller` / `ConnectivityRoot` | Feature sample | Installer registers services/presenters; root owns panel navigation, address and the shared factory |

The model is reusable across games, so it lives in the package. Presenter/view stay in the sample because navigation, text, layout and UI policy belong to the game. They can be used unchanged or customized without altering runtime contracts. No UI dependency is added to the package.

```text
ActionRunner -> IConnectionRecoveryUI (ConnectionRetryPanelGateway)
             -> PanelManager.Show -> PanelPresenterFactory
             -> ConnectionRetryPresenter <-> ConnectionRetryModel
                                        <-> IConnectionRetryView
```

A cached panel gets a fresh presenter on each opening; the model/service survive in the root. Back/Cancel disposes or signals the presenter/model and ends the pending operation. A retry within the same opening re-renders the existing presenter; it does not stack listeners.

For a game without Dreamy UI, reference only `Dreamy.Connectivity.Runtime` and `Dreamy.Connectivity.Unity`:

```csharp
var service = new ConnectivityService(new UnityReachabilityGateway());
var actions = new ConnectivityActionRunner(service, yourRecoveryUI,
    new ConnectionSettingsGateway());
// yourRecoveryUI implements IConnectionRecoveryUI using your own UI framework.
// The composition root owns/disposes service and cancels operations by its lifetime token.
```

## Quick start

Your scene needs a Canvas with one `PanelManager`, plus one EventSystem configured for its active Input System (as with other UGUI panels). The diagnostics scene supplies one; the production prefab intentionally reuses the game’s EventSystem.

Drop `ConnectivityRoot.prefab` into that scene; set its panel address; the root resolves the existing PanelManager singleton, then execute network actions through it. Initialize the shared presenter factory in GameInstaller before opening panels:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Dreamy.Connectivity;

// In your existing controller. root is a serialized ConnectivityRoot reference.
private async UniTask LoadOnlineContentAsync(CancellationToken ownerToken)
{
    var result = await root.Feature.Actions.ExecuteAsync(
        "content.load", root.CreateRequest(), LoadFromBackendAsync,
        new ConnectivityRetryPolicy(ConnectivityRetrySafety.ReadOnly), ownerToken);
    if (!result.IsSuccess)
    {
        // Handle Cancelled/RetryExhausted/Failed/OutcomeUnknown in your flow.
    }
}
```

`LoadFromBackendAsync` must return `ConnectedOperationResult`, classifying the actual backend/SDK outcome. A successful preflight never guarantees the subsequent request succeeds. Unexpected exceptions propagate to the caller; observe/log them at your app boundary. Do not report every backend error as `ConnectivityFailure`.

- `Succeeded`: confirmed operation success.
- `ConnectivityFailure`: transport failed; safe actions may retry.
- `ServiceUnavailable`: server/service error; returned to host without retrying as though the device were offline.
- `Rejected`: auth/business failure; returned to host.
- `OutcomeUnknown`: transaction may have committed; reconcile before another request. Runner never retries it automatically.

## Composition with the shared presenter factory

```csharp
// In GameInstaller, after the Canvas PanelManager and gateway are ready:
var panels = PanelManager.Instance;
// sharedFactory is the same factory used for Shop/Settings/etc.
panels.PresenterFactory = sharedFactory;
var feature = ConnectivityFeatureInstaller.Install(
    sharedFactory,
    model => new ConnectionRetryPanelGateway(panels, connectionRetryPanelAddress, model),
    lifecycle: connectivityLifecycle,
    gateway: connectivityGateway);
// Root owns feature; call feature.Dispose() on teardown.
```

Install once per factory scope. The installer accepts neither a panel address nor a PanelManager and does not resolve a singleton or replace its factory. The UI root supplies a callback that receives the feature's RetryModel and returns an IConnectionRecoveryUI. The feature owns this returned adapter: teardown closes it and disposes it when IDisposable is implemented. Custom games can return their own navigation implementation. Settings/Shop/Connectivity register in the same shared factory. `ConnectivityRoot.Initialize` provides the equivalent scene-root setup. The root requires the game's PanelManager; it does not create a hidden global UI owner.

## Use the team's SDK

Disable automatic root initialization and initialize once in GameInstaller:

```csharp
root.Initialize(new DelegateConnectivityGateway(() => TeamSdk.Internet_IsConnected()));
```

`TeamSdk` is a placeholder for your own SDK type, not a package dependency. The delegate maps true to **NetworkAvailable**, not EndpointReachable. Do not use a bool reachability gateway with `EndpointRequired`. For a backend endpoint, configure the root's HTTPS health URL, exact status and body (default 204/empty), or inject `HttpEndpointConnectivityGateway`. No third-party/public health URL is contacted by default.

For multiple services implement `IConnectivityGateway` routing `TargetId` to their adapters, then use one `ConnectivityService`. Evidence/cache are isolated by target and requirement. The supplied root represents one configured target; custom multi-target roots should map resume checks to the active target or invalidate without rendering a target-specific resume message.

## Retry policy and cancellation

Default policy is `Never`: the first dispatch is permitted after a successful check, but a dispatched unsafe operation will not retry. Preflight failure can still be retried before any dispatch.

- `ReadOnly`: allow retry for fetches.
- `IdempotentWithKey`: requires a stable host key. Your callback must actually send the same key **and immutable payload** to a backend which enforces idempotency. The package cannot enforce server deduplication.
- Defaults: 3 additional manual retry/check opportunities; automatic retries off. Opt in to at most `maxRetries` automatic attempts with bounded exponential delay/jitter.
- Budgets cap both recovery probes and dispatches. No infinite Retry loop. One foreground session per runner; another caller receives Busy.
- Pass the screen/flow owner token. The feature root also cancels active work on dispose. Cancel remains available during recovery probes.
- The runner awaits the operation callback's terminal outcome; it does not detach a mutation when the token fires. A callback that has already committed should return success, or OutcomeUnknown if it cannot determine the result. Token cancellation alone cannot roll back a backend transaction.
- Call the Unity gateways, runner and view from Unity's main thread. Shared probe state is guarded against timeout completion; cancellation of one caller does not cancel a shared check while another still waits.

Service caches positive evidence for 3s and negative for 1s by default, timeout 5s; constructors allow overrides. Manual retry forces fresh evidence. Resume invalidates cache and rechecks the root's target; it never executes an action automatically.

## Settings

Android: API 29+ Internet settings panel, then documented wireless/Wi-Fi/general settings fallbacks. JNI launch runs on Android UI thread. The app does not toggle Wi-Fi or request location permissions. Ensure your game player manifest permits Internet when using the HTTP adapter.

On iOS the public API opens **app settings**, not a guaranteed Wi-Fi page. The native `.mm` bridge is iOS-only, uses a retained AOT callback and ignores canceled request callbacks. The button is labeled Open App Settings. No private Settings URL scheme is used. Native linking and settings behavior still require Xcode/device verification; see [VALIDATION.md](VALIDATION.md).

Editor/desktop: settings unsupported, button hidden. Retry/Cancel still work. Unity reachability on desktop is only a hint; use an endpoint gateway to validate backend reachability.

References: [Unity reachability](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-internetReachability.html), [Android settings panel](https://developer.android.com/reference/android/provider/Settings.Panel), [Apple app settings](https://developer.apple.com/documentation/uikit/uiapplication/opensettingsurlstring).

## Diagnostics

Import **Connectivity Diagnostics** separately to test Loss, Recovery, Timeout, Server Error, Unknown Outcome and Cancel. It uses the same production service/presenter/prefab with a simulated gateway. Keep this sample out of release scenes/builds. The production sample does not reference its assembly.

Open `Scenes/ConnectivityDiagnostics.unity`, Play, choose **Connection Loss → Run Action → Recover Connection → Retry**. Result should be Succeeded, attempts 1. On this sandbox, disable “Play from Bootstrap” or select this scene as the Play Mode start scene; otherwise the existing toolbar starts the game bootstrap instead.

The diagnostics scene references the panel prefab in Connectivity Feature: import Feature first. Samples ship ready-made prefabs and a diagnostics scene; no Editor builder is included. Customize project-owned prefab variants. Source of truth is `Samples~`.

## Assemblies and validation

- `Dreamy.Connectivity.Runtime`: contracts, check/cache service and action runner, no UnityEngine/Editor reference.
- `Dreamy.Connectivity.Unity`: reachability/HTTP, settings and lifecycle.
- `Dreamy.Connectivity.UI`: imported Feature sample only; panel, presenter and UI composition.
- `Dreamy.Connectivity.Feature`: imported production composition/root.
- `Dreamy.Connectivity.Diagnostics`: imported development controls.
- `Dreamy.Connectivity.Tests`: runtime tests, no dependency on the Feature sample.
- `Dreamy.Connectivity.UI.Tests`: imported Feature sample UI/prefab tests, Editor-only.

Assembly references should include only types directly used by that consumer. The sample Feature and Diagnostics assembly names must be unique in the consumer; import each sample once or move the complete folder with Unity Move and `.meta` intact.

Run Unity EditMode tests in `Dreamy.Connectivity.Tests`; after importing Feature, also run `Dreamy.Connectivity.UI.Tests`. Then:

```bash
python3 LocalPackages/com.dreamy.connectivity/Tests~/validate-connectivity.py --compile-platforms
# When imported samples intentionally differ from their source, append --allow-imported-customizations.
```

The harness validates source/import equality, metadata and runtime boundaries, and compiles C# Android/iOS branches against the consumer's generated project references. It does **not** build a native player or replace device testing. See [VALIDATION.md](VALIDATION.md) for actual results and unverified gates.

## Migration: UI moved into the sample

Previous local revisions shipped `Integration/UI` and `Editor` in the package. UI scripts now live in `Samples~/Connectivity Feature/Runtime/UI`; UI tests are in the same sample. Editor builders have been removed. Import Feature before compiling game code that uses `ConnectivityRoot`, `ConnectivityFeature` or `ConnectionRetryPanel`. Assembly names, namespaces and script GUIDs are retained, preserving prefab bindings. Do not keep an extra copy of the previous UI assembly in the project. Core/network consumers need no UI migration. No separate UI UPM package is introduced.

For a fresh runtime-only consumer check (no UI packages):

```bash
python3 LocalPackages/com.dreamy.connectivity/Tests~/validate-headless-consumer.py --unity /path/to/Unity
```

This uses the consumer's resolved local UniTask and creates an isolated temporary project. Unity needs access to its normal license/cache; the fixture log and lock file are retained at the printed path.

## Migration: PanelManager MVP

`ConnectionRecoveryPresenter` is now `ConnectionRetryPresenter`; `IConnectionRecoveryView` is now `IConnectionRetryView`. Their script metadata is retained. The presenter implements `IPanelPresenter`; the runtime `IConnectionRecoveryUI` contract is unchanged. `ConnectivityFeatureInstaller.Install` now takes the shared factory and a recovery UI creation callback. Replace old address/manager overloads with the composition example above.

Root/diagnostic serialization uses the string `retryPanelAddress`; the UI root resolves PanelManager from its existing singleton, without an Inspector reference. Install after UI bootstrap (the supplied roots use Start). The installer itself can run without a scene PanelManager when supplied with a custom recovery UI. Older imported samples should use the current supplied prefabs/scene and remove obsolete pre-instantiated Retry panels. The diagnostics-only panel variant leaves simulation buttons clickable; production retains its full modal blocker.

## Retry panel Addressables path

Root Inspector uses Retry Panel Address. Only the UI root and ConnectionRetryPanelGateway know the string address and PanelManager. Pass a callback constructing that adapter to the installer, as shown above. Existing consumers passing an address or manager to Install must move them into that callback. Install registers the presenter without loading UI; the supplied adapter calls `PanelManager.Show<ConnectionRetryPanel>(address)` during recovery. PanelManager and its asset loader retain their existing loading/caching ownership.

Add the production Retry prefab to a **local** Addressables group as `Panel/ConnectionRetryPanel.prefab`. The diagnostics variant uses `UI/ConnectionRetryDiagnosticsPanel` (separate scene/manager scope). These keys are defaults, not hard-coded requirements: set the root field or pass your game's key. Rebuild Addressables content/catalog for player builds. A recovery panel stored only remotely may fail to load precisely when offline, so ship its prefab and dependencies locally. Importing samples does not modify another project's Addressables settings; register those entries in the consuming project. The sandbox entries are configured in Connectivity Samples (Local).
