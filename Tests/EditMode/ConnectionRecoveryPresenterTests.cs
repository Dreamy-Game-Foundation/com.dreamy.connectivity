using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
namespace Dreamy.Connectivity.Tests
{
    public sealed class ConnectionRecoveryPresenterTests
    {
        private sealed class View : IConnectionRecoveryView
        {
            public event Action<ConnectionRecoveryDecision> DecisionRequested;
            public event Action Closed;
            public bool Visible, Busy;
            public void Render(ConnectionRecoveryPrompt prompt, bool settingsSupported, string label) { }
            public void SetBusy(bool busy) => Busy = busy;
            public void SetStatus(string message) { }
            public void SetVisible(bool visible) { Visible = visible; if (!visible) Closed?.Invoke(); }
            public void Decide(ConnectionRecoveryDecision decision) => DecisionRequested?.Invoke(decision);
        }
        [UnityTest] public IEnumerator ReopenDoesNotDuplicateDecisions() => UniTask.ToCoroutine(async () =>
        {
            var view = new View(); using var presenter = new ConnectionRecoveryPresenter(view, null);
            for (int i=0; i<20; i++)
            {
                var task = presenter.ShowAsync(new ConnectionRecoveryPrompt("test", ConnectivityStatus.NoNetwork, true, 0), default).AsTask();
                Assert.That(view.Visible, Is.True); Assert.That(view.Busy, Is.False);
                view.Decide(ConnectionRecoveryDecision.Retry); view.Decide(ConnectionRecoveryDecision.Cancel);
                Assert.That(await task, Is.EqualTo(ConnectionRecoveryDecision.Retry));
                Assert.That(view.Busy, Is.True);
                presenter.Close(); Assert.That(view.Visible, Is.False);
            }
        });
        [UnityTest] public IEnumerator DisposeCompletesWaiterAndUnsubscribes() => UniTask.ToCoroutine(async () =>
        {
            var view = new View(); var presenter = new ConnectionRecoveryPresenter(view, null);
            var task = presenter.ShowAsync(new ConnectionRecoveryPrompt("test", ConnectivityStatus.NoNetwork, true, 0), default).AsTask();
            presenter.Dispose(); presenter.Dispose();
            Assert.That(await task, Is.EqualTo(ConnectionRecoveryDecision.Cancel)); Assert.That(view.Visible, Is.False);
            view.Decide(ConnectionRecoveryDecision.Retry);
            Assert.Throws<ObjectDisposedException>(() => presenter.ShowAsync(default, default).GetAwaiter().GetResult());
        });
    }
}
