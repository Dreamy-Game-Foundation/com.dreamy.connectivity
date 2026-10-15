# Connectivity validation — 2026-10-15

Local implementation `0.1.0`, Unity `6000.4.12f1`, Linux Editor / StandaloneLinux64.
Package source is maintained in its own Git repository and consumed as a pinned submodule. No release tag has been published. Toolkit compatibility registry was not available in this checkout. No native mobile release claim is made from these checks.

## Passed

- Unity compile: Runtime, Unity integration, UI integration, Editor tooling, Feature and Diagnostics sample assemblies.
- Unity EditMode: **27 passed / 0 failed / 0 skipped**, job `4021267b72484d1ca28ac17711eada19`.
- Tests cover evidence mapping, cache/TTL, target and requirement isolation, force refresh, shared waiter cancellation, timeout, invalidation during in-flight probe, owner lifetime, Busy, safe/unsafe retry, budgets/delay, settings without implicit dispatch, exceptions, unknown outcomes, presenter reopen/disposal and wrong target.
- Prefabs and scene created with Unity Editor APIs, serialized view references inspected, no Missing Script/unassigned required panel field.
- Native plugin importer: iOS enabled; Any Platform, Editor and Android disabled.
- Standard Package Manager `Sample.Import(OverridePreviousImports)` returned true for Connectivity Feature and Connectivity Diagnostics. Both samples compile together after reimport in this sandbox. Source `Samples~` and imported assets checked for byte equality.
- Play Mode button flow: Connection Loss → Run Action → Recover Connection → Retry returned `Succeeded; attempts: 1; total calls: 1`.
- Play Mode Server Error returned `Failed; attempts: 1; simulated_503` without an offline retry loop.
- Play Mode Unknown Outcome returned `OutcomeUnknown; attempts: 1`, without repeat dispatch.
- Play Mode Probe Timeout displayed “The connection check took too long. Please try again.”; Cancel returned `Cancelled; attempts: 0; total calls: 0`.
- Portrait Game view inspected at 1170 × 2532. [Diagnostics screenshot](Documentation~/ConnectivityDiagnostics.png).
- `python3 LocalPackages/com.dreamy.connectivity/Tests~/validate-connectivity.py --compile-platforms`: package boundaries, sample declarations/equality/metadata and C# `UNITY_ANDROID` / `UNITY_IOS` branches passed against actual Unity references. These are managed branch compiles, not native player builds.
- Original active BootstrapScene and its Play Mode start-scene setting restored after checks. No existing scene authored by the game was saved by this task.

## Remaining platform/release gates

- A separate clean consumer project import/compile; current sample reimport was in this sandbox.
- Android IL2CPP/player build, device Internet-panel intent fallback, OEM differences, physical loss/recovery and app focus transitions.
- iOS Xcode native bridge compile/link, AOT callback and app-settings behavior on iPhone. C# compilation does not validate Objective-C++ linking or OS behavior.
- Landscape/tablet/safe-area device layout pass and actual host-owned HTTPS health service contract. The HTTP gateway does not contact any endpoint unless the host configures it.
- Store/backend transaction reconciliation is a host responsibility. A successful reachability probe is not proof of a successful purchase.

Production sample code uses real gateways and contains no fake success path. Its production release readiness still depends on the host's endpoint/SDK, UI customization and the platform gates above. Diagnostics is development-only and should be excluded from release scenes.
