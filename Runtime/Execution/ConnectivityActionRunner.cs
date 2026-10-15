using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    /// <summary>One foreground recovery session. Never retries unknown transaction outcomes.</summary>
    public sealed class ConnectivityActionRunner
    {
        private readonly IConnectivityService connectivity;
        private readonly IConnectionRecoveryUI ui;
        private readonly IConnectionSettingsGateway settings;
        private readonly IConnectivityTiming timing;
        private readonly CancellationToken lifetime;
        private bool busy;
        public ConnectivityActionRunner(IConnectivityService connectivity, IConnectionRecoveryUI ui,
            IConnectionSettingsGateway settings = null, IConnectivityTiming timing = null, CancellationToken lifetime = default)
        {
            this.connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
            this.ui = ui; this.settings = settings; this.lifetime = lifetime; this.timing = timing ?? new SystemConnectivityTiming();
        }
        public async UniTask<ConnectivityActionResult> ExecuteAsync(string operationId, ConnectivityCheckRequest request,
            Func<CancellationToken, UniTask<ConnectedOperationResult>> operation, ConnectivityRetryPolicy policy,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(operationId)) throw new ArgumentException("Operation ID is required.", nameof(operationId));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            cancellationToken.ThrowIfCancellationRequested();
            if (busy) return new ConnectivityActionResult(ConnectivityActionStatus.Busy);
            busy = true;
            using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime);
            cancellationToken = session.Token;
            void CancelSession() => session.Cancel();
            if (ui != null) ui.CancelRequested += CancelSession;
            int attempts = 0, probes = 0, automatic = 0;
            string message = null, lastError = null;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ConnectivityCheckResult check = await connectivity.CheckAsync(request, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    probes++;
                    ConnectivityStatus failure = check.Status;
                    if (check.Satisfies(request.Requirement))
                    {
                        attempts++;
                        // Do not detach cancellation from a mutation: await its terminal result.
                        ConnectedOperationResult result = await operation(cancellationToken);
                        if (result.Status == ConnectedOperationStatus.Succeeded)
                            return new ConnectivityActionResult(ConnectivityActionStatus.Succeeded, attempts);
                        if (result.Status == ConnectedOperationStatus.OutcomeUnknown)
                            return new ConnectivityActionResult(ConnectivityActionStatus.OutcomeUnknown, attempts, result.ErrorCode);
                        if (result.Status == ConnectedOperationStatus.Rejected || result.Status == ConnectedOperationStatus.ServiceUnavailable)
                            return new ConnectivityActionResult(ConnectivityActionStatus.Failed, attempts, result.ErrorCode);
                        if (result.Status != ConnectedOperationStatus.ConnectivityFailure)
                            return new ConnectivityActionResult(ConnectivityActionStatus.Failed, attempts, "invalid_operation_status");
                        failure = ConnectivityStatus.EndpointUnavailable;
                        lastError = result.ErrorCode; message = null;
                        connectivity.Invalidate();
                    }
                    if (failure == ConnectivityStatus.Misconfigured)
                        return new ConnectivityActionResult(ConnectivityActionStatus.Failed, attempts, check.ErrorCode ?? "misconfigured");
                    bool canRetry = (attempts == 0 || policy.Safety != ConnectivityRetrySafety.Never) &&
                        attempts <= policy.MaxRetries && probes <= policy.MaxRetries + 1;
                    if (canRetry && automatic < policy.AutoRetries)
                    {
                        // Bounded jitter, no global random state dependency.
                        double jitter = (operationId.GetHashCode() & 255) / 1024.0;
                        await timing.DelayAsync(TimeSpan.FromSeconds(Math.Pow(2, automatic++) + jitter), cancellationToken);
                        request = request.Refresh(); continue;
                    }
                    if (ui == null) return new ConnectivityActionResult(ConnectivityActionStatus.Failed, attempts, lastError ?? check.ErrorCode ?? "recovery_ui_unavailable");
                    ConnectionRecoveryDecision decision = await ui.ShowAsync(
                        new ConnectionRecoveryPrompt(operationId, failure, canRetry, attempts, message), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (decision == ConnectionRecoveryDecision.Cancel)
                        return new ConnectivityActionResult(canRetry ? ConnectivityActionStatus.Cancelled : ConnectivityActionStatus.RetryExhausted, attempts);
                    while (decision == ConnectionRecoveryDecision.OpenSettings)
                    {
                        OpenConnectionSettingsStatus opened = settings == null ? OpenConnectionSettingsStatus.Unsupported : await settings.OpenAsync(cancellationToken);
                        message = opened == OpenConnectionSettingsStatus.Opened ? "settings_opened" : "settings_failed";
                        connectivity.Invalidate();
                        // Settings and resume never execute the pending action automatically.
                        decision = await ui.ShowAsync(new ConnectionRecoveryPrompt(operationId, failure, canRetry, attempts, message), cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (decision == ConnectionRecoveryDecision.Cancel) return new ConnectivityActionResult(ConnectivityActionStatus.Cancelled, attempts);
                    }
                    if (!canRetry) return new ConnectivityActionResult(ConnectivityActionStatus.RetryExhausted, attempts);
                    request = request.Refresh();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { return new ConnectivityActionResult(ConnectivityActionStatus.Cancelled, attempts); }
            finally { if (ui != null) ui.CancelRequested -= CancelSession; busy = false; ui?.Close(); }
        }
    }
}
