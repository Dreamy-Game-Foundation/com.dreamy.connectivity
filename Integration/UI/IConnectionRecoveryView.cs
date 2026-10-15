using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
namespace Dreamy.Connectivity
{
    public interface IConnectionRecoveryView
    {
        event Action<ConnectionRecoveryDecision> DecisionRequested;
        event Action Closed;
        void Render(ConnectionRecoveryPrompt prompt, bool settingsSupported, string settingsLabel);
        void SetBusy(bool busy);
        void SetStatus(string message);
        void SetVisible(bool visible);
    }
}
