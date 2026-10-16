# Changelog

## [0.1.1] - 2026-10-16

- Make the Diagnostics scene own and toggle its Retry view directly, without PanelManager or Addressables navigation.
- Separate simulation controls from the recovery dialog, use responsive action rows, and preserve button bindings in an independent diagnostics prefab.
- Refresh connectivity per action, report simulation recovery without dispatching automatically, and restore Run after owner re-enable.
- Verify 35 Connectivity EditMode tests and manual recovery/cancel/timeout/server-error/unknown-outcome flows; existing UI presenter test failures and native Settings device verification remain open.

## [0.1.0] - 2026-10-16

- Consolidate the current implementation for a local source release; clean UI consumer checks passed; native/mobile gates and the full-project UI test failures remain open.

## Unreleased — PanelManager MVP (2026-10-16)
- Replace direct Retry prefab references with Addressables address strings in root, installer, feature and panel gateway; samples use local UI/ConnectionRetryPanel and UI/ConnectionRetryDiagnosticsPanel keys.
- Add optional scene-entry check and development offline-on-entry checkbox to ConnectivityRoot, plus CheckConnectivityAsync, CheckConnectionAsync and ExecuteWhenConnectedAsync helpers.
- Add ConnectivityRoot development simulation controls and optional Inspector buttons for loss/recovery, timeout, live gateway and action cancellation; no bootstrap gate.

- Add engine-independent ConnectionRetryModel/State to runtime for recovery state/decisions.
- Rename presenter/view contracts consistently; presenter now implements IPanelPresenter and is created by the shared PanelPresenterFactory.
- Replace direct view activation with ConnectionRetryPanelGateway using PanelManager.Show/Close.
- Migrate root/diagnostic assets to prefab references; add diagnostics-only panel variant and migration menu.
- Test pending cancellation, model state, presenter recreation, cached panel reopen and actual manager Back.

## Earlier local changes — optional sample UI (2026-10-16)

- Move panel/presenter/UI composition, Editor tools and UI tests into Connectivity Feature sample.
- Remove Dreamy UI, Dreamy Feature and UGUI from package dependencies; document manual sample prerequisites.
- Retry panel is a BaseFeaturePanel variant; retain script GUIDs and assembly names.
- Split runtime and imported UI test assemblies; game-owned imported prefab/scene customizations are preserved.

## 0.1.0 — 2026-10-15

- Injectable Unity reachability, SDK delegate and host-owned HTTPS endpoint checks.
- Target-scoped cache, probe deduplication, independent waiter cancellation and timeout handling.
- Foreground action recovery with bounded manual/automatic retry, safety policy, Busy and unknown-outcome handling.
- Android settings fallback, iOS public app-settings native bridge and resume recheck.
- Production Canvas modal, root/installer sample and separate loss/recovery diagnostics scene.
- Regression tests, Unity-created prefabs, authoring validator and platform C# compile harness.

Initial local implementation; native player/device release gates are tracked in VALIDATION.md. No remote tag published.
