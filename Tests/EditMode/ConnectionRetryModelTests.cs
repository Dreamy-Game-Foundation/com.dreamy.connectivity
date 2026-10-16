using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
namespace Dreamy.Connectivity.Tests
{
    public sealed class ConnectionRetryModelTests
    {
        private static ConnectionRecoveryPrompt Prompt(bool canRetry = true) => new("read", ConnectivityStatus.NoNetwork, canRetry, 0);
        [UnityTest] public IEnumerator StateAndDecisionAreIndependentOfViewLifetime() => UniTask.ToCoroutine(async () =>
        {
            using var model = new ConnectionRetryModel(); int changes=0; model.StateChanged += ()=>changes++;
            var decision = model.WaitForDecisionAsync(Prompt(), default).AsTask();
            Assert.That(model.State.IsWaiting, Is.True); Assert.That(model.State.Prompt.OperationId, Is.EqualTo("read"));
            model.ReportConnectionAvailability(true); Assert.That(model.State.ConnectionAvailable, Is.True);
            model.Decide(ConnectionRecoveryDecision.Retry);
            Assert.That(await decision, Is.EqualTo(ConnectionRecoveryDecision.Retry)); Assert.That(model.State.IsWaiting, Is.False);
            Assert.That(changes, Is.GreaterThanOrEqualTo(3));
        });
        [UnityTest] public IEnumerator DisabledRetryCannotCompleteWaiter() => UniTask.ToCoroutine(async () =>
        {
            using var model = new ConnectionRetryModel(); var decision=model.WaitForDecisionAsync(Prompt(false),default).AsTask();
            model.Decide(ConnectionRecoveryDecision.Retry); Assert.That(decision.IsCompleted, Is.False);
            model.Decide(ConnectionRecoveryDecision.OpenSettings); Assert.That(await decision, Is.EqualTo(ConnectionRecoveryDecision.OpenSettings));
        });
        [UnityTest] public IEnumerator CancelAndDisposeCompletePendingWaiter() => UniTask.ToCoroutine(async () =>
        {
            var model = new ConnectionRetryModel(); int cancellations=0; model.CancelRequested += ()=>cancellations++;
            var decision=model.WaitForDecisionAsync(Prompt(),default).AsTask(); model.Dispose(); model.Dispose();
            Assert.That(await decision, Is.EqualTo(ConnectionRecoveryDecision.Cancel)); Assert.That(cancellations, Is.EqualTo(1));
        });
        [UnityTest] public IEnumerator CallerCancellationDoesNotLeaveBusyState() => UniTask.ToCoroutine(async () =>
        {
            using var model = new ConnectionRetryModel(); using var token = new CancellationTokenSource();
            var first=model.WaitForDecisionAsync(Prompt(),token.Token).AsTask(); token.Cancel();
            try { await first; Assert.Fail("Expected cancellation."); } catch(OperationCanceledException) { }
            Assert.That(model.State.IsWaiting, Is.False);
            var next=model.WaitForDecisionAsync(Prompt(),default).AsTask(); model.Decide(ConnectionRecoveryDecision.Cancel);
            Assert.That(await next, Is.EqualTo(ConnectionRecoveryDecision.Cancel));
        });
    }
}
