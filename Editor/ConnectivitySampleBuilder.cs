using System;
using System.IO;
using System.Linq;
using Dreamy.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dreamy.Connectivity.Editor
{
    /// <summary>Builds assets through Unity APIs; never edits prefab/scene YAML.</summary>
    public static class ConnectivitySampleBuilder
    {
        private const string Package = "LocalPackages/com.dreamy.connectivity";
        private const string ImportRoot = "Assets/Samples/Dreamy Connectivity/0.1.0";
        private static readonly Color Ink = new(0.06f, 0.09f, 0.16f, 1);
        private static readonly Color Accent = new(0.16f, 0.49f, 0.94f, 1);

        [MenuItem("Tools/Dreamy/Connectivity/Import Authoring Samples")]
        public static void ImportAuthoringSamples()
        {
            foreach (string name in new[] { "Connectivity Feature", "Connectivity Diagnostics" })
            {
                string source = Package + "/Samples~/" + name;
                string destination = ImportRoot + "/" + name;
                if (Directory.Exists(destination)) throw new InvalidOperationException("Sample already exists: " + destination);
                CopyDirectory(source, destination);
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Dreamy/Connectivity/Build Sample Assets")]
        public static void BuildSampleAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var oldScene = SceneManager.GetActiveScene();
            Scene authoring = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(authoring);
            try
            {
                BuildFeature();
                BuildDiagnostics();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                SceneManager.SetActiveScene(oldScene);
                EditorSceneManager.CloseScene(authoring, true);
            }
            AssetDatabase.Refresh();
            Validate();
        }

        private static void BuildFeature()
        {
            string folder = ImportRoot + "/Connectivity Feature";
            Directory.CreateDirectory(folder + "/Prefabs");
            GameObject panel = BuildPanel();
            var panelPrefab = PrefabUtility.SaveAsPrefabAsset(panel, folder + "/Prefabs/ConnectionRetryPanel.prefab");
            UnityEngine.Object.DestroyImmediate(panel);
            var root = new GameObject("ConnectivityRoot");
            var lifecycle = root.AddComponent<ConnectivityLifecycle>();
            Component component = root.AddComponent(FindType("Dreamy.Connectivity.ConnectivityRoot"));
            GameObject panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, root.transform);
            Assign(component, "retryPanel", panelInstance.GetComponent<ConnectionRetryPanel>());
            Assign(component, "lifecycle", lifecycle);
            PrefabUtility.SaveAsPrefabAsset(root, folder + "/Prefabs/ConnectivityRoot.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BuildDiagnostics()
        {
            string folder = ImportRoot + "/Connectivity Diagnostics";
            Directory.CreateDirectory(folder + "/Scenes");
            // Authoring scene contains only our own objects. The user's active scene stays loaded and untouched.
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
            var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional;
            var events = new GameObject("EventSystem", typeof(EventSystem));
            Type input = FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule", false);
            if (input != null)
            {
                Component module = events.AddComponent(input);
                input.GetMethod("AssignDefaultActions")?.Invoke(module, null);
            }
            else events.AddComponent<StandaloneInputModule>();
            var canvas = Canvas("ConnectivityDiagnosticsCanvas", 0);
            var safe = Rect("SafeArea", canvas.transform); safe.gameObject.AddComponent<SafeArea>(); Stretch(safe);
            var body = Rect("Controls", safe); body.anchorMin = new Vector2(0.08f, 0.08f); body.anchorMax = new Vector2(0.92f, 0.92f); body.offsetMin = body.offsetMax = Vector2.zero;
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            Label("Heading", body, "CONNECTIVITY DIAGNOSTICS", 32, 70);
            var status = Label("Status", body, "Development controls. The production sample contains no network simulation.", 22, 90);
            var controls = Rect("Actions", body);
            controls.gameObject.AddComponent<LayoutElement>().preferredHeight = 400;
            var grid = controls.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(270, 82); grid.spacing = new Vector2(12,12); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            Component diagnostics = canvas.AddComponent(FindType("Dreamy.Connectivity.Diagnostics.ConnectivityDiagnostics"));
            var lifecycle = canvas.AddComponent<ConnectivityLifecycle>();
            Assign(diagnostics, "statusText", status); Assign(diagnostics, "lifecycle", lifecycle);
            foreach (var item in new[] { ("runButton", "Run Action"), ("lossButton", "Connection Loss"), ("recoveryButton", "Recover Connection"),
                ("timeoutButton", "Probe Timeout"), ("serverErrorButton", "Server Error"), ("unknownOutcomeButton", "Unknown Outcome"), ("cancelButton", "Cancel Action") })
                Assign(diagnostics, item.Item1, Button(item.Item1, controls, item.Item2, Accent));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImportRoot + "/Connectivity Feature/Prefabs/ConnectionRetryPanel.prefab");
            GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // Diagnostics must leave controls usable to simulate recovery while the modal is open.
            panel.GetComponent<Canvas>().sortingOrder = 10;
            var panelRect = panel.GetComponent<RectTransform>();
            var dialog = panel.transform.Find("SafeArea/Dialog").GetComponent<RectTransform>();
            dialog.anchorMin = new Vector2(0.08f, 0.04f); dialog.anchorMax = new Vector2(0.92f, 0.42f); dialog.offsetMin = dialog.offsetMax = Vector2.zero;
            panel.transform.Find("Backdrop").GetComponent<Image>().raycastTarget = false;
            Assign(diagnostics, "retryPanel", panel.GetComponent<ConnectionRetryPanel>());
            panel.SetActive(false);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), folder + "/Scenes/ConnectivityDiagnostics.unity");
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(go);
        }

        private static GameObject BuildPanel()
        {
            GameObject root = Canvas("ConnectionRetryPanel", 1000);
            var view = root.AddComponent<ConnectionRetryPanel>();
            RectTransform backdrop = Rect("Backdrop", root.transform); Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0,0,0,0.72f);
            var safe = Rect("SafeArea", root.transform); Stretch(safe); safe.gameObject.AddComponent<SafeArea>();
            var dialog = Rect("Dialog", safe);
            dialog.anchorMin = new Vector2(0.08f,0.5f); dialog.anchorMax = new Vector2(0.92f,0.5f); dialog.sizeDelta = new Vector2(0, 580); dialog.anchoredPosition = Vector2.zero;
            dialog.gameObject.AddComponent<Image>().color = Ink;
            var layout = dialog.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28,28,24,24); layout.spacing = 14; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            Assign(view, "titleText", Label("Title", dialog, "Connection unavailable", 30, 52));
            Assign(view, "messageText", Label("Message", dialog, "Check your connection, then try again.", 23, 85));
            var progress = Label("Progress", dialog, "Checking connection...", 19, 28).gameObject; progress.SetActive(false); Assign(view, "progress", progress);
            Assign(view, "retryButton", Button("RetryButton", dialog, "Retry", Accent));
            var settings = Button("SettingsButton", dialog, "Open Connection Settings", new Color(0.19f,0.23f,0.33f));
            Assign(view, "settingsButton", settings); Assign(view, "settingsLabel", settings.GetComponentInChildren<TMP_Text>());
            Assign(view, "cancelButton", Button("CancelButton", dialog, "Cancel", new Color(0.19f,0.23f,0.33f)));
            root.SetActive(false);
            return root;
        }
        private static GameObject Canvas(string name, int order)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720,1280); scaler.matchWidthOrHeight = 0.5f;
            return root;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static TMP_Text Label(string name, Transform parent, string value, float size, float height)
        {
            var rect = Rect(name,parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            if (text.font == null) throw new InvalidOperationException("Import TMP Essential Resources before building samples.");
            text.text = value; text.fontSize = size; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = height; element.minHeight = height;
            return text;
        }
        private static Button Button(string name, Transform parent, string text, Color color)
        {
            var rect = Rect(name,parent); var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 66; element.minHeight = 60;
            var label = Label("Label", rect, text, 22, 60); Stretch(label.rectTransform); return button;
        }
        private static void Assign(Component component, string field, UnityEngine.Object value)
        { var serialized = new SerializedObject(component); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        private static Type FindType(string name, bool required = true)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
            if (required && type == null) throw new InvalidOperationException("Import samples and wait for compilation: " + name);
            return type;
        }
        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
        [MenuItem("Tools/Dreamy/Connectivity/Export Authoring Samples")]
        public static void ExportSamples()
        {
            foreach (string name in new[] { "Connectivity Feature", "Connectivity Diagnostics" })
                CopyDirectory(ImportRoot + "/" + name, Package + "/Samples~/" + name);
        }
        [MenuItem("Tools/Dreamy/Connectivity/Validate Sample Assets")]
        public static void Validate()
        {
            string path = ImportRoot + "/Connectivity Feature/Prefabs/ConnectionRetryPanel.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing production panel prefab.");
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0) throw new InvalidOperationException("Missing script: " + child.name);
            var view = new SerializedObject(prefab.GetComponent<ConnectionRetryPanel>());
            foreach (string name in new[] { "titleText", "messageText", "retryButton", "settingsButton", "settingsLabel", "cancelButton", "progress" })
                if (view.FindProperty(name).objectReferenceValue == null) throw new InvalidOperationException("Unassigned field: " + name);
            Debug.Log("PASS Connectivity production panel references and missing-script validation.");
        }
    }
}
