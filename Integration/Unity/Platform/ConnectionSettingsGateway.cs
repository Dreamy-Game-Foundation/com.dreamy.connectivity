using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    public sealed class ConnectionSettingsGateway : IConnectionSettingsGateway
    {
        public bool IsSupported => Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
        public string ButtonLabel => Application.platform == RuntimePlatform.IPhonePlayer ? "Open App Settings" : "Open Connection Settings";
        public async UniTask<OpenConnectionSettingsStatus> OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if UNITY_ANDROID && !UNITY_EDITOR
            var completion = new UniTaskCompletionSource<OpenConnectionSettingsStatus>();
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    bool opened = false;
                    try
                    {
                        using var version = new AndroidJavaClass("android.os.Build$VERSION");
                        string[] actions = version.GetStatic<int>("SDK_INT") >= 29
                            ? new[] { "android.settings.panel.action.INTERNET_CONNECTIVITY", "android.settings.WIRELESS_SETTINGS", "android.settings.WIFI_SETTINGS", "android.settings.SETTINGS" }
                            : new[] { "android.settings.WIRELESS_SETTINGS", "android.settings.WIFI_SETTINGS", "android.settings.SETTINGS" };
                        foreach (string action in actions)
                        {
                            try { using var intent = new AndroidJavaObject("android.content.Intent", action); activity.Call("startActivity", intent); opened = true; break; }
                            catch (AndroidJavaException) { /* Try the next documented OS destination. */ }
                        }
                    }
                    catch (Exception exception) { UniTask.Post(() => Debug.LogException(exception)); }
                    finally { activity.Dispose(); UniTask.Post(() => completion.TrySetResult(opened ? OpenConnectionSettingsStatus.Opened : OpenConnectionSettingsStatus.Failed)); }
                }));
            }
            return await completion.Task.AttachExternalCancellation(cancellationToken);
#elif UNITY_IOS && !UNITY_EDITOR
            return await IosConnectionSettings.OpenAsync(cancellationToken);
#else
            await UniTask.CompletedTask;
            return OpenConnectionSettingsStatus.Unsupported;
#endif
        }
    }
}
