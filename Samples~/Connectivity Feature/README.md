# Connectivity Feature

Production integration sample. Import TMP Essential Resources before importing this sample. It contains a root prefab and a complete Retry modal prefab; it has no diagnostics simulation or SDK stub.

The game scene must have one EventSystem configured for its input backend; the prefab reuses it.

1. Place `Prefabs/ConnectivityRoot.prefab` into the scene.
2. Its `ConnectivityRoot` initializes on Start. Leave health URL blank for reachability, or supply your team's HTTPS endpoint and expected status/body.
3. Pass your screen cancellation token to `root.Feature.Actions.ExecuteAsync`. Use `root.CreateRequest()` so target/requirement match the configured gateway.
4. To inject the team's SDK: disable Initialize On Start, call `root.Initialize(new DelegateConnectivityGateway(() => YourSdk.Internet_IsConnected()))` from GameInstaller.
5. Replace serialized text/font/colors in a project-owned variant. Forward your UI manager's Back action to `ConnectionRetryPanel.RequestBack`.

Keep exactly one foreground recovery root per game UI scope. Root owns/disposes the service, presenter and lifetime; share it across callers. Do not create a root per button. Use the installer directly if your existing GameInstaller owns the dependencies.

For custom UI, replace `IConnectionRecoveryUI`; for custom backend/SDK, replace `IConnectivityGateway`. See the package README for operation result classification, idempotency and platform limitations. The supplied panel uses a standalone Canvas to work before panel navigation is initialized.
