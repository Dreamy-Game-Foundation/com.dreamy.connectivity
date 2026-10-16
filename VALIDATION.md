# v0.1.1 source release review — 2026-10-16

- Diagnostics owns its Retry instance/model/presenter directly; no PanelManager or Addressables required by the diagnostics scene.
- Connectivity Runtime/UI EditMode: 35/35 passed, job `7e56e8810c7d4a5fadc3a628e9de562d`.
- Manual Play Mode checks passed: offline preflight, retry after recovery exactly once, cancel, timeout, server error, unknown outcome, and disable/re-enable cleanup.
- Imported and distributable Diagnostics source/assets match. Package boundary/metadata validation passed.
- Expanded UI/Connectivity run had the same five UI PanelPresenterTests failures reproduced with the original SafeArea code (baseline job `71670e48942445d59fde267b334b0057`). Native OS Settings remains unverified on devices.
- Historical review entries below describe previous baselines.

---

# Local source tag v0.1.0 review — 2026-10-16

- Targeted package Unity EditMode suites: 128/128 passed, no skips, job `3ac312a96d2f4c54955357f20b834ab8` (combined Economy/Shop/Progression/Connectivity suites).
- Clean runtime-only consumer passed at `/tmp/dreamy-connectivity-headless-v9mex0v3`.
- Clean UI consumer compiled the four packages and imported unmodified source samples; prefab script/binding checks passed at `/tmp/dreamy-resource-consumer-xul2ly5o`.
- Android/iOS managed branches passed again; native builds and devices remain unverified.
- Full-project EditMode found five failures in the unchanged Dreamy UI PanelPresenterTests. This tag identifies source and does not certify an entirely green project or production deployment.
- The later clean UI consumer check supersedes the older pending clean-import gate below. Historical Play Mode/device evidence is not a new run.

---

# Connectivity validation — updated 2026-10-16

Local implementation `0.1.0`, Unity `6000.4.12f1`, Linux Editor / StandaloneLinux64.
Package source is maintained in its own Git repository and consumed as a pinned submodule. No release tag has been published. Toolkit compatibility registry was not available in this checkout. No native mobile release claim is made from these checks.

## Installer navigation separation — 2026-10-16

- ConnectivityFeatureInstaller accepts the shared PanelPresenterFactory and a `Func<ConnectionRetryModel, IConnectionRecoveryUI>` creation callback. Neither the installer nor ConnectivityFeature accepts or resolves PanelManager or a panel address.
- ConnectivityRoot and the diagnostics root own manager resolution, shared-factory assignment and construction of the supplied ConnectionRetryPanelGateway. Source and imported sample code/docs/tests are synchronized; imported panel art is retained.
- Sandbox Unity 6000.4.12f1: script refresh completed, Console contained no errors; **35 EditMode passed / 0 failed / 0 skipped**, MCP job `9cbd635b0f69469593a0060cf77a835e`. Includes three new composition/cancellation/validation tests and existing domain/presenter/prefab regressions.
- `python3 LocalPackages/com.dreamy.connectivity/Tests~/validate-connectivity.py --allow-imported-customizations` passed. The supplied Retry panel's existing imported customization was retained. Play Mode navigation and device builds were not rerun for this refactor.

## PanelManager MVP — 2026-10-16

- `ConnectionRetryModel` / `ConnectionRetryState` are engine-independent runtime types. Presenter/view/navigation remain sample-only; package dependencies still exclude Dreamy UI/Feature/UGUI.
- `ConnectionRetryPresenter : IPanelPresenter` is registered with the shared PanelPresenterFactory and instantiated by UIPanel/PanelManager during Show. `ConnectionRetryPanelGateway` uses PanelManager.Show/Close; the view has no SetVisible or presenter construction.
- Root and diagnostic assets migrated with Unity APIs to prefab references. Production panel keeps its BaseFeaturePanel variant; diagnostics uses a child variant to leave simulation controls clickable. No prefab/scene YAML edited manually. Pre-migration sandbox asset backups: `/tmp/dreamy-connectivity-mvp-backup`.
- Unity EditMode **32 passed / 0 failed / 0 skipped**, job `a48bfca5bd20462d8fd999deacf0a61b`. Includes model decision/cancellation, 20 factory/host reopen cycles, presenter unbind/cancel and prefab reference checks.
- Actual Play Mode: loss/run created a managed ConnectionRetryPanel with ConnectionRetryPresenter, verified via its PanelPresenterHost. Recovery/Retry returned Succeeded, attempts 1 / calls 1; panel became inactive and presenter host was released.
- Actual cached reopen created a different presenter instance. The manager's Back handler was invoked and returned Cancelled, attempts 0, without dispatching another operation.
- `validate-connectivity.py --allow-imported-customizations --compile-platforms` passed metadata/boundaries and Android/iOS C# branches.
- `validate-headless-consumer.py --unity /home/tutrinh/Unity/Hub/Editor/6000.4.12f1/Editor/Unity` passed fresh consumer compile and gateway execution without UI packages, fixture `/tmp/dreamy-connectivity-headless-vnk662zi`.
- Console had no C# compilation errors after migration; original Play Mode start scene restored. Native/device gates below remain unchanged.

## UI sample separation — 2026-10-16

- Package manifest depends only on UniTask, UnityWebRequest and AndroidJNI. Dreamy UI, Dreamy Feature and UGUI are optional sample prerequisites.
- All panel/view/presenter/UI composition, Editor authoring and UI test code moved under `Samples~/Connectivity Feature`; script `.meta` GUIDs and assembly names retained. Core tests have no reference to sample/UI assemblies.
- Retry prefab is a validated variant of `Packages/com.dreamy.feature/Runtime/Prefabs/BaseFeaturePanel.prefab`. Its required UI/tween references passed the imported prefab test.
- Unity compile passed for runtime/platform and imported Feature/Diagnostics/UI/Editor/test assemblies.
- **28 tests passed / 0 failed / 0 skipped** across `Dreamy.Connectivity.Tests` and `Dreamy.Connectivity.UI.Tests`, job `7c459ebc77814116aa22b2d11d3694f8`.
- `validate-connectivity.py --allow-imported-customizations --compile-platforms` passed boundaries, sample metadata, and C# Android/iOS compilation without referencing any Dreamy assemblies or UGUI/TMP. Existing host-owned root prefab/diagnostic scene differences were reported and preserved.
- Fresh Unity consumer at `/tmp/dreamy-connectivity-headless-iir79_f2` had only local Connectivity and UniTask as direct dependencies. Unity batchmode compile/execution exited 0 and logged `PASS_HEADLESS_CONNECTIVITY_NO_UI: NetworkAvailable`. The fixture lock contains no Dreamy UI, Dreamy Feature or UGUI. Native/player behavior was not tested by this check.
- Reproduce clean core validation with `python3 LocalPackages/com.dreamy.connectivity/Tests~/validate-headless-consumer.py --unity /path/to/Unity`; requires a licensed Editor. The ordinary sandbox cannot access the host license/cache, so the successful batchmode run used the approved unsandboxed execution.

The earlier 2026-10-15 evidence below records the original implementation; the layout and dependencies above supersede the original package-level UI arrangement.

## Initial implementation evidence (2026-10-15)

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

- Clean UI-sample consumer import/compile remains to run; the separate runtime-only consumer passed on 2026-10-16.
- Android IL2CPP/player build, device Internet-panel intent fallback, OEM differences, physical loss/recovery and app focus transitions.
- iOS Xcode native bridge compile/link, AOT callback and app-settings behavior on iPhone. C# compilation does not validate Objective-C++ linking or OS behavior.
- Landscape/tablet/safe-area device layout pass and actual host-owned HTTPS health service contract. The HTTP gateway does not contact any endpoint unless the host configures it.
- Store/backend transaction reconciliation is a host responsibility. A successful reachability probe is not proof of a successful purchase.

Production sample code uses real gateways and contains no fake success path. Its production release readiness still depends on the host's endpoint/SDK, UI customization and the platform gates above. Diagnostics is development-only and should be excluded from release scenes.

## Addressables path migration — 2026-10-16

Retry UI now opens by string address through PanelManager.Show. Unity 6000.4.12f1 batchmode compiled the project and migrated the supplied root prefab/diagnostic scene via Editor APIs. Exit code 0; marker PASS_CONNECTIVITY_PATH_MIGRATION_AND_LOCAL_ADDRESSES in /tmp/connectivity-path-migration.log. Both sample keys resolve to prefabs containing ConnectionRetryPanel in a local Addressables group. Temporary migration script removed afterward; imported panel art/customizations retained. Structural validator passed with imported customizations allowed. Play Mode loading and player content build were not rerun after the Editor session ended.
