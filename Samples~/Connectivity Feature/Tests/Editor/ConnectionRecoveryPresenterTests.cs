using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
namespace Dreamy.Connectivity.Tests
{
    public sealed class ConnectionRecoveryPresenterTests
    {
        private sealed class View : IConnectionRetryView
        {
            public event Action<ConnectionRecoveryDecision> DecisionRequested;
            public ConnectionRetryState State;
            public int Renders;
            public void Render(ConnectionRetryState state, bool supported, string label) { State=state; Renders++; }
            public void Decide(ConnectionRecoveryDecision decision) => DecisionRequested?.Invoke(decision);
        }
        [Test] public void ImportedRetryPrefabUsesFeatureFoundationAndKeepsBindings()
        {
            const string path = "Assets/Samples/Dreamy Connectivity/0.1.0/Connectivity Feature/Prefabs/ConnectionRetryPanel.prefab";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
            if (prefab == null) Assert.Ignore("Import Connectivity Feature to run its prefab integration check.");
            Assert.That(UnityEditor.PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(UnityEditor.PrefabAssetType.Variant));
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(prefab)),
                Is.EqualTo("Packages/com.dreamy.feature/Runtime/Prefabs/BaseFeaturePanel.prefab"));
            var panel = prefab.GetComponent<ConnectionRetryPanel>();
            Assert.That(panel, Is.InstanceOf<Dreamy.UI.UIPanel>());
            var fields = new UnityEditor.SerializedObject(panel);
            foreach (string field in new[] { "canvasGroup", "tweenPlayer", "titleText", "messageText", "retryButton", "settingsButton", "settingsLabel", "cancelButton", "progress" })
                Assert.That(fields.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
        [UnityTest] public IEnumerator FactoryCreatesPresenterPerCachedOpening() => UniTask.ToCoroutine(async () =>
        {
            using var model = new ConnectionRetryModel(); var view = new View();
            var factory = new Dreamy.UI.PanelPresenterFactory(); int creations=0;
            factory.Register<View>(v=> {creations++; return new ConnectionRetryPresenter(model,v);});
            using var host = new Dreamy.UI.PanelPresenterHost(factory, view);
            for(int n=0;n<20;n++)
            {
                var decision=model.WaitForDecisionAsync(new ConnectionRecoveryPrompt("test",ConnectivityStatus.NoNetwork,true,0),default).AsTask();
                host.Show(); host.Show(); Assert.That(creations, Is.EqualTo(n+1)); Assert.That(view.State.IsWaiting, Is.True);
                view.Decide(ConnectionRecoveryDecision.Retry); Assert.That(await decision, Is.EqualTo(ConnectionRecoveryDecision.Retry));
                host.Dispose(); int renders=view.Renders;
                model.ReportConnectionAvailability(true); Assert.That(view.Renders, Is.EqualTo(renders));
            }
        });
        [UnityTest] public IEnumerator ClosingPresenterCancelsPendingRecovery() => UniTask.ToCoroutine(async () =>
        {
            using var model=new ConnectionRetryModel(); var view=new View();
            var decision=model.WaitForDecisionAsync(new ConnectionRecoveryPrompt("test",ConnectivityStatus.NoNetwork,true,0),default).AsTask();
            var presenter=new ConnectionRetryPresenter(model,view); presenter.Show(); presenter.Dispose(); presenter.Dispose();
            Assert.That(await decision, Is.EqualTo(ConnectionRecoveryDecision.Cancel));
            int renders=view.Renders; model.ReportConnectionAvailability(true); Assert.That(view.Renders, Is.EqualTo(renders));
        });
    }
}
