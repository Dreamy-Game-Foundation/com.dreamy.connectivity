using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Attach once to the root; coalesces focus and pause resume notifications.</summary>
    public sealed class ConnectivityLifecycle : MonoBehaviour
    {
        private IConnectivityService service;
        private int resumeFrame = -1;
        public event Action Resumed;
        public void Initialize(IConnectivityService service) => this.service = service ?? throw new ArgumentNullException(nameof(service));
        private void OnApplicationPause(bool paused) { if (!paused) Resume(); }
        private void OnApplicationFocus(bool focused) { if (focused) Resume(); }
        private void Resume()
        {
            if (service == null || resumeFrame == Time.frameCount) return;
            resumeFrame = Time.frameCount; service.Invalidate(); Resumed?.Invoke();
        }
        private void OnDestroy() { service = null; Resumed = null; }
    }
}
