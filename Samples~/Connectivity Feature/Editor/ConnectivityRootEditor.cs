using UnityEditor;
using UnityEngine;

namespace Dreamy.Connectivity.Editor
{
    [CustomEditor(typeof(ConnectivityRoot))]
    public sealed class ConnectivityRootEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var root = (ConnectivityRoot)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Connection Simulation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(root.SimulationStatus, MessageType.Info);
            using (new EditorGUI.DisabledScope(!root.CanSimulate || root.Feature == null))
            {
                if (GUILayout.Button("Lose Connection + Run Test")) root.SimulateConnectionLoss();
                if (GUILayout.Button("Recover Connection (then press Retry)")) root.SimulateConnectionRecovery();
                if (GUILayout.Button("Simulate Timeout + Run Test")) root.SimulateConnectionTimeout();
                if (GUILayout.Button("Use Live Gateway")) root.UseLiveGateway();
                using (new EditorGUI.DisabledScope(root.IsSimulationActionRunning))
                    if (GUILayout.Button("Run Test Action")) root.RunSimulationAction();
                using (new EditorGUI.DisabledScope(!root.IsSimulationActionRunning))
                    if (GUILayout.Button("Cancel Test Action")) root.CancelSimulationAction();
            }
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode and initialize the root to use simulation controls.", MessageType.None);
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
