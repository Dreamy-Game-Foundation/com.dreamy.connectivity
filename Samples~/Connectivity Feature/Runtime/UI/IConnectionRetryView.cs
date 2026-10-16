using System;
namespace Dreamy.Connectivity
{
    public interface IConnectionRetryView
    {
        event Action<ConnectionRecoveryDecision> DecisionRequested;
        void Render(ConnectionRetryState state, bool settingsSupported, string settingsLabel);
    }
}
