using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    /// <summary>Production entry point. Call from GameInstaller after creating your Canvas/view.</summary>
    public static class ConnectivityFeatureInstaller
    {
        public static ConnectivityFeature Install(ConnectionRetryPanel panel, ConnectivityLifecycle lifecycle,
            IConnectivityGateway gateway = null, string targetId = "default",
            ConnectivityRequirement requirement = ConnectivityRequirement.NetworkHintOnly,
            IConnectionSettingsGateway settings = null) =>
            new(gateway ?? new UnityReachabilityGateway(), panel, targetId, requirement, settings, lifecycle);
    }
}
