using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Exact status/body check on a host-owned HTTPS endpoint. No redirects.</summary>
    public sealed class HttpEndpointConnectivityGateway : IConnectivityGateway
    {
        private readonly string targetId, url, expectedBody;
        private readonly long expectedStatus;
        private readonly int timeoutSeconds;
        private readonly IConnectivityGateway network;
        public HttpEndpointConnectivityGateway(string targetId, string url, long expectedStatus = 204,
            string expectedBody = "", int timeoutSeconds = 5, IConnectivityGateway network = null)
        {
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Target ID is required.", nameof(targetId));
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Configure a host-owned HTTPS health endpoint.", nameof(url));
            if (expectedStatus < 200 || expectedStatus > 299 || timeoutSeconds < 1) throw new ArgumentOutOfRangeException(nameof(expectedStatus));
            this.targetId = targetId; this.url = url; this.expectedStatus = expectedStatus;
            this.expectedBody = expectedBody; this.timeoutSeconds = timeoutSeconds; this.network = network ?? new UnityReachabilityGateway();
        }
        public async UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.TargetId != targetId) return new ConnectivityCheckResult(request.TargetId, ConnectivityStatus.Misconfigured, "unknown_target");
            var hint = await network.CheckAsync(request, cancellationToken);
            if (hint.Status == ConnectivityStatus.NoNetwork || request.Requirement == ConnectivityRequirement.NetworkHintOnly) return hint;
            using var web = UnityEngine.Networking.UnityWebRequest.Get(url);
            web.timeout = timeoutSeconds; web.redirectLimit = 0;
            try { await web.SendWebRequest().ToUniTask(cancellationToken: cancellationToken); }
            catch (UnityWebRequestException) { } // Status is classified below, not swallowed as success.
            cancellationToken.ThrowIfCancellationRequested();
            bool success = web.result == UnityEngine.Networking.UnityWebRequest.Result.Success && web.responseCode == expectedStatus &&
                (expectedBody == null || string.Equals(web.downloadHandler.text, expectedBody, StringComparison.Ordinal));
            return new ConnectivityCheckResult(request.TargetId, success ? ConnectivityStatus.EndpointReachable : ConnectivityStatus.EndpointUnavailable,
                success ? null : "http_" + web.responseCode);
        }
    }
}
