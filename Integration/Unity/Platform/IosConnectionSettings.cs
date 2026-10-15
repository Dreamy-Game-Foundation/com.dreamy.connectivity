using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
#if UNITY_IOS && !UNITY_EDITOR
    internal static class IosConnectionSettings
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate void OpenCallback(int requestId, int opened);
        private static readonly OpenCallback callback = Complete;
        private static readonly System.Collections.Generic.Dictionary<int, UniTaskCompletionSource<OpenConnectionSettingsStatus>> pending = new();
        private static int sequence;
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void DreamyConnectivity_OpenSettings(int requestId, OpenCallback completion);
        internal static async UniTask<OpenConnectionSettingsStatus> OpenAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int id = ++sequence;
            var completion = new UniTaskCompletionSource<OpenConnectionSettingsStatus>();
            pending.Add(id, completion);
            try
            {
                DreamyConnectivity_OpenSettings(id, callback);
                return await completion.Task.AttachExternalCancellation(token);
            }
            finally { pending.Remove(id); }
        }
        [AOT.MonoPInvokeCallback(typeof(OpenCallback))]
        private static void Complete(int requestId, int opened) => UniTask.Post(() =>
        {
            if (pending.TryGetValue(requestId, out var completion))
                completion.TrySetResult(opened != 0 ? OpenConnectionSettingsStatus.Opened : OpenConnectionSettingsStatus.Failed);
        });
    }
#endif
}
