using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Ready-to-use scene root. Initialize once; dispose the feature when this root is destroyed.</summary>
    [DisallowMultipleComponent]
    public sealed class ConnectivityRoot : MonoBehaviour
    {
        [SerializeField] private ConnectionRetryPanel retryPanel;
        [SerializeField] private ConnectivityLifecycle lifecycle;
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField] private string targetId = "default";
        [Tooltip("Leave blank for device reachability. Set a host-owned HTTPS health URL to require endpoint health.")]
        [SerializeField] private string healthUrl;
        [SerializeField] private int expectedStatus = 204;
        [SerializeField] private string expectedBody = "";
        public ConnectivityFeature Feature { get; private set; }
        private void Start() { if (initializeOnStart && Feature == null) Initialize(); }
        public void Initialize(IConnectivityGateway gateway = null)
        {
            if (Feature != null) throw new InvalidOperationException("ConnectivityRoot is already initialized.");
            bool endpoint = !string.IsNullOrWhiteSpace(healthUrl);
            gateway ??= endpoint ? new HttpEndpointConnectivityGateway(targetId, healthUrl, expectedStatus, expectedBody) : new UnityReachabilityGateway();
            Feature = ConnectivityFeatureInstaller.Install(retryPanel, lifecycle, gateway, targetId,
                endpoint ? ConnectivityRequirement.EndpointRequired : ConnectivityRequirement.NetworkHintOnly);
        }
        public ConnectivityCheckRequest CreateRequest() => new(targetId, string.IsNullOrWhiteSpace(healthUrl)
            ? ConnectivityRequirement.NetworkHintOnly : ConnectivityRequirement.EndpointRequired);
        private void OnDestroy() { Feature?.Dispose(); Feature = null; }
    }
}
