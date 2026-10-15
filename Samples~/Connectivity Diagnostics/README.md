# Connectivity Diagnostics

Development-only simulation UI using the production Connectivity service and Retry panel. Import **Connectivity Feature first**; this scene references its Retry panel prefab. Do not include this scene in release builds.

Open `Scenes/ConnectivityDiagnostics.unity`, enter Play Mode, and use:

- **Connection Loss → Run Action**: panel appears without executing the action.
- **Recover Connection → Retry**: action succeeds exactly once.
- **Probe Timeout → Run Action**: after the 5-second check deadline, panel offers retry/cancel. Select recovery before retrying.
- **Server Error → Run Action**: Failed, no misleading “device offline” popup.
- **Unknown Outcome → Run Action**: OutcomeUnknown, no automatic duplicate transaction.
- **Cancel Action** or panel Cancel: stop the pending recovery session.

Choose a state before Run Action. Changing state while an operation runs also tests loss after preflight. Diagnostic controls stay clickable while the sample popup is visible so recovery can be simulated without toggling your actual network. The production prefab uses a full-screen input blocker.

Settings is hidden on unsupported desktop/Editor platforms. Test its OS behavior on an Android/iOS device. If this sandbox starts BootstrapScene when pressing Play, choose the diagnostics scene as Play Mode Start Scene or turn off Play from Bootstrap in the Dreamy toolbar.
