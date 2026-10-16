using System;
using Dreamy.UI;
namespace Dreamy.Connectivity
{
    public static class ConnectivityFeatureInstaller
    {
        /// <summary>Register with the game's shared factory. The UI root supplies recovery navigation.</summary>
        public static ConnectivityFeature Install(PanelPresenterFactory factory,
            Func<ConnectionRetryModel, IConnectionRecoveryUI> createRecoveryUI,
            ConnectivityLifecycle lifecycle = null, IConnectivityGateway gateway = null, string targetId = "default",
            ConnectivityRequirement requirement = ConnectivityRequirement.NetworkHintOnly,
            IConnectionSettingsGateway settings = null)
        {
            return new ConnectivityFeature(gateway ?? new UnityReachabilityGateway(), factory, createRecoveryUI,
                targetId, requirement, settings, lifecycle);
        }
    }
}
