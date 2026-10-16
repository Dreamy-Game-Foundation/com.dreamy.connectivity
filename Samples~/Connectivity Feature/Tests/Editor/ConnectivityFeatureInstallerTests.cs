using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dreamy.UI;
using NUnit.Framework;

namespace Dreamy.Connectivity.Tests
{
    public sealed class ConnectivityFeatureInstallerTests
    {
        private sealed class OfflineGateway : IConnectivityGateway
        {
            public UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken token)
                => UniTask.FromResult(new ConnectivityCheckResult(request.TargetId, ConnectivityStatus.NoNetwork));
        }
        private sealed class RecoveryUI : IConnectionRecoveryUI, IDisposable
        {
            public event Action CancelRequested;
            public int Shows, Closes, Disposes;
            public CancellationToken Token;
            public UniTaskCompletionSource<ConnectionRecoveryDecision> Pending;
            public UniTask<ConnectionRecoveryDecision> ShowAsync(ConnectionRecoveryPrompt prompt, CancellationToken token)
            {
                Shows++; Token = token;
                return Pending == null ? UniTask.FromResult(ConnectionRecoveryDecision.Cancel)
                    : Pending.Task.AttachExternalCancellation(token);
            }
            public void Close() => Closes++;
            public void Dispose() => Disposes++;
        }
        private sealed class ExistingView { }
        private sealed class ExistingPresenter : IPanelPresenter
        {
            public void Show() { }
            public void Dispose() { }
        }

        [Test]
        public async Task InstallUsesSharedFactoryAndInjectedUI_WithoutSceneManagerOrAddress()
        {
            var factory = new PanelPresenterFactory();
            var existing = new ExistingPresenter();
            factory.Register<ExistingView>(_ => existing);
            var ui = new RecoveryUI();
            ConnectionRetryModel suppliedModel = null;
            using var feature = ConnectivityFeatureInstaller.Install(factory, model =>
            {
                suppliedModel = model;
                return ui;
            }, gateway: new OfflineGateway());
            Assert.That(suppliedModel, Is.SameAs(feature.RetryModel));
            Assert.That(factory.Create(new ExistingView()), Is.SameAs(existing));
            var result = await feature.Actions.ExecuteAsync("test", new ConnectivityCheckRequest("default"),
                _ => throw new AssertionException("Offline operation must not run."), new ConnectivityRetryPolicy());
            Assert.That(result.Status, Is.EqualTo(ConnectivityActionStatus.Cancelled));
            Assert.That(ui.Shows, Is.EqualTo(1));
            Assert.That(ui.Closes, Is.EqualTo(1));
        }

        [Test]
        public async Task DisposeCancelsPendingInjectedUI_AndDisposesItOnce()
        {
            var ui = new RecoveryUI { Pending = new UniTaskCompletionSource<ConnectionRecoveryDecision>() };
            var feature = ConnectivityFeatureInstaller.Install(new PanelPresenterFactory(), _ => ui, gateway: new OfflineGateway());
            var action = feature.Actions.ExecuteAsync("test", new ConnectivityCheckRequest("default"),
                _ => throw new AssertionException("Offline operation must not run."), new ConnectivityRetryPolicy());
            Assert.That(ui.Shows, Is.EqualTo(1));
            feature.Dispose(); feature.Dispose();
            Assert.That((await action).Status, Is.EqualTo(ConnectivityActionStatus.Cancelled));
            Assert.That(ui.Token.IsCancellationRequested, Is.True);
            Assert.That(ui.Disposes, Is.EqualTo(1));
        }

        [Test]
        public void MissingFactoryOrRecoveryUIFailsAtComposition()
        {
            Assert.Throws<ArgumentNullException>(() => ConnectivityFeatureInstaller.Install(null, _ => new RecoveryUI(), gateway: new OfflineGateway()));
            Assert.Throws<ArgumentNullException>(() => ConnectivityFeatureInstaller.Install(new PanelPresenterFactory(), null, gateway: new OfflineGateway()));
            Assert.Throws<InvalidOperationException>(() => ConnectivityFeatureInstaller.Install(new PanelPresenterFactory(), _ => null, gateway: new OfflineGateway()));
        }
    }
}
