# Dreamy Connectivity

Reusable connectivity checks, foreground action recovery and Android/iOS settings.
Version `0.1.0`, Unity 6000.0+. Domain is engine independent; gateways and UI are replaceable.

## Install and import

Install `package.json` from disk, or use a pinned commit from `https://github.com/Dreamy-Game-Foundation/com.dreamy.connectivity.git`. This sandbox uses the Git submodule at `LocalPackages/com.dreamy.connectivity` through `file:../LocalPackages/com.dreamy.connectivity`. No release tag has been published; native/device gates remain in VALIDATION.md.

Dependencies: UniTask 2.5.10+, Dreamy UI 1.0.5, UGUI 2.0.0, UnityWebRequest and AndroidJNI modules. This UPM package includes UI integration, so its UPM UI dependency is required even when your assembly references only the domain.

Import TMP Essential Resources, then Package Manager → Dreamy Connectivity → **Connectivity Feature**. This is the production integration: no fake gateway, fake server, seeded transaction or diagnostics controls. Prefabs use the standard LiberationSans SDF from TMP Essential Resources; replace fonts/art/text for your game.

- `Prefabs/ConnectivityRoot.prefab`: scene root, lifecycle bridge and nested Retry panel, using device reachability by default.
- `Prefabs/ConnectionRetryPanel.prefab`: complete Canvas modal, safe area, buttons, text and serialized references.
- `ConnectivityRoot`: initializes on Start by default. For your own GameInstaller, disable `Initialize On Start` and call `Initialize` at your chosen Ready stage.
- `ConnectivityFeatureInstaller`: explicit composition API when you already own a Canvas/view/root.

The Canvas view deliberately has no dependency on a particular game's panel navigation or `PanelManager.Show`. This keeps a modal available even before game panels initialize. To use Dreamy panel navigation or another UI framework, implement `IConnectionRecoveryUI` or host `IConnectionRecoveryView`; forward your Back action to `ConnectionRetryPanel.RequestBack`. Replacing presentation does not change the check/retry domain.

## Quick start

Your scene needs one EventSystem configured for its active Input System (as with other UGUI panels). The diagnostics scene supplies one; the production prefab intentionally reuses the game’s EventSystem.

Drop `ConnectivityRoot.prefab` into your scene, assign its component to your controller, and execute network actions through it:

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

The diagnostics scene references the panel prefab in Connectivity Feature: import Feature first. The package sample builder provides Tools → Dreamy → Connectivity menus for authoring/validation/export. It operates on the designated sample folders; do not rebuild over customized imported assets. Source of truth is `Samples~`.

## Assemblies and validation

- `Dreamy.Connectivity.Runtime`: contracts, check/cache service and action runner, no UnityEngine/Editor reference.
- `Dreamy.Connectivity.Unity`: reachability/HTTP, settings and lifecycle.
- `Dreamy.Connectivity.UI`: Canvas view, pure presenter and root-owned feature.
- `Dreamy.Connectivity.Feature`: imported production composition/root.
- `Dreamy.Connectivity.Diagnostics`: imported development controls.
- `Dreamy.Connectivity.Editor`: prefab authoring and reference validator, Editor-only.

Assembly references should include only types directly used by that consumer. The sample Feature and Diagnostics assembly names must be unique in the consumer; import each sample once or move the complete folder with Unity Move and `.meta` intact.

Run Unity EditMode tests in `Dreamy.Connectivity.Tests`, then:

```bash
python3 LocalPackages/com.dreamy.connectivity/Tests~/validate-connectivity.py --compile-platforms
```

The harness validates source/import equality, metadata and runtime boundaries, and compiles C# Android/iOS branches against the consumer's generated project references. It does **not** build a native player or replace device testing. See [VALIDATION.md](VALIDATION.md) for actual results and unverified gates.
