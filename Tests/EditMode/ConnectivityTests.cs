using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Dreamy.Connectivity.Tests
{
    public sealed class ConnectivityTests
    {
        private sealed class Gateway : IConnectivityGateway
        {
            public int Calls;
            public ConnectivityStatus Status = ConnectivityStatus.NetworkAvailable;
            public UniTaskCompletionSource<ConnectivityCheckResult> Pending;
            public UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken token)
            {
                Calls++; token.ThrowIfCancellationRequested();
                return Pending == null ? UniTask.FromResult(new ConnectivityCheckResult(request.TargetId, Status)) : Pending.Task;
            }
        }
        private sealed class Timing : IConnectivityTiming
        {
            public double Now;
            public int Delays;
            public double Seconds => Now;
            public UniTask DelayAsync(TimeSpan delay, CancellationToken token) { token.ThrowIfCancellationRequested(); Now += delay.TotalSeconds; Delays++; return UniTask.CompletedTask; }
        }
        private sealed class Recovery : IConnectionRecoveryUI
        {
            public event Action CancelRequested;
            public int Shows, Closes;
            public ConnectionRecoveryPrompt Prompt;
            public readonly Queue<ConnectionRecoveryDecision> Decisions = new();
            public Action OnShow;
            public UniTask<ConnectionRecoveryDecision> ShowAsync(ConnectionRecoveryPrompt prompt, CancellationToken token)
            {
                token.ThrowIfCancellationRequested(); Shows++; Prompt = prompt; OnShow?.Invoke();
                return UniTask.FromResult(Decisions.Count > 0 ? Decisions.Dequeue() : ConnectionRecoveryDecision.Cancel);
            }
            public void Close() { Closes++; }
            public void Cancel() => CancelRequested?.Invoke();
        }
        private sealed class Settings : IConnectionSettingsGateway
        {
            public int Calls;
            public bool IsSupported => true;
            public string ButtonLabel => "Settings";
            public UniTask<OpenConnectionSettingsStatus> OpenAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Calls++; return UniTask.FromResult(OpenConnectionSettingsStatus.Opened); }
        }
        private static readonly ConnectivityCheckRequest Request = new("backend");
        private static UniTask<ConnectedOperationResult> Success(CancellationToken token) => UniTask.FromResult(ConnectedOperationResult.Success());

        [Test] public void SdkTrueDoesNotProveBackend()
        {
            var result = new DelegateConnectivityGateway(() => true).CheckAsync(Request, default).GetAwaiter().GetResult();
            Assert.That(result.Satisfies(ConnectivityRequirement.NetworkHintOnly), Is.True);
            Assert.That(result.Satisfies(ConnectivityRequirement.EndpointRequired), Is.False);
        }
        [Test] public void FalseSdkIsNoNetwork() => Assert.That(new DelegateConnectivityGateway(() => false).CheckAsync(Request, default).GetAwaiter().GetResult().Status, Is.EqualTo(ConnectivityStatus.NoNetwork));
        [Test] public void CacheExpiresAndForceRefreshBypassesIt()
        {
            var gateway = new Gateway(); var timing = new Timing(); using var service = new ConnectivityService(gateway, timing);
            service.CheckAsync(Request, default).GetAwaiter().GetResult();
            Assert.That(service.CheckAsync(Request, default).GetAwaiter().GetResult().FromCache, Is.True);
            service.CheckAsync(Request.Refresh(), default).GetAwaiter().GetResult(); Assert.That(gateway.Calls, Is.EqualTo(2));
            timing.Now = 4; service.CheckAsync(Request, default).GetAwaiter().GetResult(); Assert.That(gateway.Calls, Is.EqualTo(3));
            service.Invalidate(); service.CheckAsync(Request, default).GetAwaiter().GetResult(); Assert.That(gateway.Calls, Is.EqualTo(4));
        }
        [Test] public void TargetsAndRequirementsDoNotShareEvidence()
        {
            var gateway = new Gateway(); using var service = new ConnectivityService(gateway);
            service.CheckAsync(Request, default).GetAwaiter().GetResult();
            service.CheckAsync(new ConnectivityCheckRequest("other"), default).GetAwaiter().GetResult();
            service.CheckAsync(new ConnectivityCheckRequest("backend", ConnectivityRequirement.EndpointRequired), default).GetAwaiter().GetResult();
            Assert.That(gateway.Calls, Is.EqualTo(3));
        }
        [Test] public void RequestFirstDoesNotProbe()
        {
            var gateway = new Gateway(); using var service = new ConnectivityService(gateway);
            service.CheckAsync(new ConnectivityCheckRequest("backend", ConnectivityRequirement.RequestFirst), default).GetAwaiter().GetResult();
            Assert.That(gateway.Calls, Is.Zero);
        }
        [Test] public void RetryPolicyRejectsUnsafeAutoRetryAndMissingKey()
        {
            Assert.Throws<ArgumentException>(() => new ConnectivityRetryPolicy(ConnectivityRetrySafety.Never, autoRetries: 1));
            Assert.Throws<ArgumentException>(() => new ConnectivityRetryPolicy(ConnectivityRetrySafety.IdempotentWithKey));
        }
        [Test] public void OfflineRecoveryRunsActionExactlyOnce()
        {
            var gateway = new Gateway { Status = ConnectivityStatus.NoNetwork }; using var service = new ConnectivityService(gateway);
            var ui = new Recovery(); ui.Decisions.Enqueue(ConnectionRecoveryDecision.Retry); ui.OnShow = () => gateway.Status = ConnectivityStatus.NetworkAvailable;
            int calls = 0;
            var result = new ConnectivityActionRunner(service, ui).ExecuteAsync("upgrade", Request,
                _ => { calls++; return Success(default); }, new ConnectivityRetryPolicy()).GetAwaiter().GetResult();
            Assert.That(result.IsSuccess, Is.True); Assert.That(calls, Is.EqualTo(1)); Assert.That(ui.Closes, Is.EqualTo(1));
        }
        [Test] public void PreflightPassDoesNotHideRequestFailure()
        {
            using var service = new ConnectivityService(new Gateway()); var ui = new Recovery(); ui.Decisions.Enqueue(ConnectionRecoveryDecision.Retry);
            int calls = 0;
            var result = new ConnectivityActionRunner(service, ui).ExecuteAsync("read", Request,
                _ => UniTask.FromResult(++calls == 1 ? new ConnectedOperationResult(ConnectedOperationStatus.ConnectivityFailure) : ConnectedOperationResult.Success()),
                new ConnectivityRetryPolicy(ConnectivityRetrySafety.ReadOnly)).GetAwaiter().GetResult();
            Assert.That(result.IsSuccess, Is.True); Assert.That(calls, Is.EqualTo(2));
        }
        [TestCase(ConnectedOperationStatus.OutcomeUnknown, ConnectivityActionStatus.OutcomeUnknown)]
        [TestCase(ConnectedOperationStatus.ServiceUnavailable, ConnectivityActionStatus.Failed)]
        [TestCase(ConnectedOperationStatus.Rejected, ConnectivityActionStatus.Failed)]
        public void NonTransportFailuresNeverRetry(ConnectedOperationStatus status, ConnectivityActionStatus expected)
        {
            using var service = new ConnectivityService(new Gateway()); var ui = new Recovery();
            var result = new ConnectivityActionRunner(service, ui).ExecuteAsync("purchase", Request, _ => UniTask.FromResult(new ConnectedOperationResult(status)),
                new ConnectivityRetryPolicy(ConnectivityRetrySafety.IdempotentWithKey, idempotencyKey: "receipt-1")).GetAwaiter().GetResult();
            Assert.That(result.Status, Is.EqualTo(expected)); Assert.That(ui.Shows, Is.Zero); Assert.That(result.Attempts, Is.EqualTo(1));
        }
        [Test] public void UnsafeOperationCannotRetryAfterDispatch()
        {
            using var service = new ConnectivityService(new Gateway()); var ui = new Recovery(); ui.Decisions.Enqueue(ConnectionRecoveryDecision.Retry);
            var result = new ConnectivityActionRunner(service, ui).ExecuteAsync("unsafe", Request, _ => UniTask.FromResult(new ConnectedOperationResult(ConnectedOperationStatus.ConnectivityFailure)),
                new ConnectivityRetryPolicy()).GetAwaiter().GetResult();
            Assert.That(result.Status, Is.EqualTo(ConnectivityActionStatus.RetryExhausted)); Assert.That(result.Attempts, Is.EqualTo(1)); Assert.That(ui.Prompt.CanRetry, Is.False);
        }
        [Test] public void AutoRetryIsBoundedAndUsesDelay()
        {
            using var service = new ConnectivityService(new Gateway()); var timing = new Timing(); var ui = new Recovery();
            var result = new ConnectivityActionRunner(service, ui, timing: timing).ExecuteAsync("read", Request,
                _ => UniTask.FromResult(new ConnectedOperationResult(ConnectedOperationStatus.ConnectivityFailure)),
                new ConnectivityRetryPolicy(ConnectivityRetrySafety.ReadOnly, 2, 2)).GetAwaiter().GetResult();
            Assert.That(result.Attempts, Is.EqualTo(3)); Assert.That(timing.Delays, Is.EqualTo(2)); Assert.That(ui.Prompt.CanRetry, Is.False);
        }
        [Test] public void SettingsDoNotExecuteActionWithoutRetry()
        {
            using var service = new ConnectivityService(new Gateway { Status = ConnectivityStatus.NoNetwork }); var ui = new Recovery(); var settings = new Settings();
            ui.Decisions.Enqueue(ConnectionRecoveryDecision.OpenSettings); ui.Decisions.Enqueue(ConnectionRecoveryDecision.OpenSettings); ui.Decisions.Enqueue(ConnectionRecoveryDecision.Cancel);
            var result = new ConnectivityActionRunner(service, ui, settings).ExecuteAsync("read", Request, Success, new ConnectivityRetryPolicy()).GetAwaiter().GetResult();
            Assert.That(result.Attempts, Is.Zero); Assert.That(settings.Calls, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator ProbeDedupAndCallerCancellation() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new Gateway { Pending = new UniTaskCompletionSource<ConnectivityCheckResult>() }; using var service = new ConnectivityService(gateway);
            using var firstCancellation = new CancellationTokenSource();
            var first = service.CheckAsync(Request, firstCancellation.Token).AsTask();
            var second = service.CheckAsync(Request, default).AsTask();
            firstCancellation.Cancel();
            bool cancelled = false; try { await first; } catch (OperationCanceledException) { cancelled = true; }
            Assert.That(cancelled, Is.True); Assert.That(gateway.Calls, Is.EqualTo(1));
            gateway.Pending.TrySetResult(new ConnectivityCheckResult("backend", ConnectivityStatus.EndpointReachable));
            Assert.That((await second).Status, Is.EqualTo(ConnectivityStatus.EndpointReachable));
        });
        [UnityTest] public IEnumerator ProbeTimeoutIsNotOffline() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new Gateway { Pending = new UniTaskCompletionSource<ConnectivityCheckResult>() }; using var service = new ConnectivityService(gateway, timeoutSeconds: 0.02);
            Assert.That((await service.CheckAsync(Request, default)).Status, Is.EqualTo(ConnectivityStatus.TimedOut));
        });
        [UnityTest] public IEnumerator BusyAndCancellationReleaseSession() => UniTask.ToCoroutine(async () =>
        {
            using var service = new ConnectivityService(new Gateway()); var ui = new Recovery(); var runner = new ConnectivityActionRunner(service, ui);
            var completion = new UniTaskCompletionSource<ConnectedOperationResult>(); using var token = new CancellationTokenSource();
            var first = runner.ExecuteAsync("one", Request, _ => completion.Task, new ConnectivityRetryPolicy(), token.Token).AsTask();
            var second = await runner.ExecuteAsync("two", Request, Success, new ConnectivityRetryPolicy());
            Assert.That(second.Status, Is.EqualTo(ConnectivityActionStatus.Busy));
            token.Cancel(); completion.TrySetCanceled(token.Token);
            Assert.That((await first).Status, Is.EqualTo(ConnectivityActionStatus.Cancelled));
            Assert.That((await runner.ExecuteAsync("three", Request, Success, new ConnectivityRetryPolicy())).IsSuccess, Is.True);
        });
        [Test] public void InvalidAndDisposedServiceFailClearly()
        {
            var service = new ConnectivityService(new Gateway()); service.Dispose();
            Assert.Throws<ObjectDisposedException>(() => service.CheckAsync(Request, default).GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => new ConnectivityCheckRequest(""));
        }

        [Test] public void CanceledOwnerDoesNotDispatch()
        {
            using var service = new ConnectivityService(new Gateway()); using var owner = new CancellationTokenSource(); owner.Cancel();
            Assert.Throws<OperationCanceledException>(() => new ConnectivityActionRunner(service, null).ExecuteAsync("cancelled", Request, Success, new ConnectivityRetryPolicy(), owner.Token).GetAwaiter().GetResult());
        }
        [Test] public void MissingRecoveryUiPreservesFailure()
        {
            using var service = new ConnectivityService(new Gateway { Status = ConnectivityStatus.NoNetwork });
            var result = new ConnectivityActionRunner(service, null).ExecuteAsync("read", Request, Success, new ConnectivityRetryPolicy()).GetAwaiter().GetResult();
            Assert.That(result.Status, Is.EqualTo(ConnectivityActionStatus.Failed)); Assert.That(result.Attempts, Is.Zero);
        }
        [Test] public void ExceptionsReleaseBusyAndPropagate()
        {
            using var service = new ConnectivityService(new Gateway()); var ui = new Recovery(); var runner = new ConnectivityActionRunner(service, ui);
            Assert.Throws<InvalidOperationException>(() => runner.ExecuteAsync("bad", Request, _ => throw new InvalidOperationException("host failure"), new ConnectivityRetryPolicy()).GetAwaiter().GetResult());
            Assert.That(runner.ExecuteAsync("next", Request, Success, new ConnectivityRetryPolicy()).GetAwaiter().GetResult().IsSuccess, Is.True);
        }
        [UnityTest] public IEnumerator CancelDuringRecoveryProbeStopsSession() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new Gateway { Status = ConnectivityStatus.NoNetwork }; using var service = new ConnectivityService(gateway);
            var ui = new Recovery(); ui.Decisions.Enqueue(ConnectionRecoveryDecision.Retry);
            ui.OnShow = () => gateway.Pending = new UniTaskCompletionSource<ConnectivityCheckResult>();
            var runner = new ConnectivityActionRunner(service, ui);
            var task = runner.ExecuteAsync("read", Request, Success, new ConnectivityRetryPolicy()).AsTask();
            ui.Cancel();
            Assert.That((await task).Status, Is.EqualTo(ConnectivityActionStatus.Cancelled));
        });
        [UnityTest] public IEnumerator RootLifetimeCancelsEvenBeforePopupOpens() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new Gateway { Pending = new UniTaskCompletionSource<ConnectivityCheckResult>() }; using var service = new ConnectivityService(gateway);
            using var owner = new CancellationTokenSource();
            var task = new ConnectivityActionRunner(service, null, lifetime: owner.Token).ExecuteAsync("read", Request, Success, new ConnectivityRetryPolicy()).AsTask();
            owner.Cancel();
            Assert.That((await task).Status, Is.EqualTo(ConnectivityActionStatus.Cancelled));
        });
        [UnityTest] public IEnumerator InvalidatedInflightEvidenceCannotRepopulateCache() => UniTask.ToCoroutine(async () =>
        {
            var gateway = new Gateway { Pending = new UniTaskCompletionSource<ConnectivityCheckResult>() }; using var service = new ConnectivityService(gateway);
            var task = service.CheckAsync(Request, default).AsTask(); service.Invalidate();
            gateway.Pending.TrySetResult(new ConnectivityCheckResult("backend", ConnectivityStatus.NetworkAvailable));
            await task; gateway.Pending = null;
            var next = await service.CheckAsync(Request, default);
            Assert.That(next.FromCache, Is.False); Assert.That(gateway.Calls, Is.EqualTo(2));
        });
        [Test] public void WrongTargetIsMisconfiguration()
        {
            var gateway = new DelegateResultGateway(new ConnectivityCheckResult("wrong", ConnectivityStatus.EndpointReachable));
            using var service = new ConnectivityService(gateway);
            Assert.That(service.CheckAsync(Request, default).GetAwaiter().GetResult().Status, Is.EqualTo(ConnectivityStatus.Misconfigured));
        }
        private sealed class DelegateResultGateway : IConnectivityGateway
        {
            private readonly ConnectivityCheckResult result;
            public DelegateResultGateway(ConnectivityCheckResult result) => this.result = result;
            public UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken token) => UniTask.FromResult(result);
        }
    }
}
