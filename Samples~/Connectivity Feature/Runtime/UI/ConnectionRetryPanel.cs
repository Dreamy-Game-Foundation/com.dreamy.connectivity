using System;
using UnityEngine;
using Dreamy.UI;
using UnityEngine.UI;
namespace Dreamy.Connectivity
{
    /// <summary>Feature panel view. Uses the shared BaseFeaturePanel prefab foundation; has no service lookup.</summary>
    [DisallowMultipleComponent]
    public sealed class ConnectionRetryPanel : UIPanel, IConnectionRetryView
    {
        [SerializeField] private TMPro.TMP_Text titleText;
        [SerializeField] private TMPro.TMP_Text messageText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMPro.TMP_Text settingsLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private GameObject progress;
        [Header("Presentation text (replace/localize per game)")]
        [SerializeField] private string offlineTitle = "Connection unavailable";
        [SerializeField] private string retryMessage = "Check your connection, then try again.";
        [SerializeField] private string exhaustedMessage = "Unable to continue. Please close and try again later.";
        [SerializeField] private string settingsOpenedMessage = "Return to the game and press Retry.";
        [SerializeField] private string settingsFailedMessage = "Settings could not be opened. Change your connection manually.";
        [SerializeField] private string timeoutMessage = "The connection check took too long. Please try again.";
        [SerializeField] private string serviceMessage = "The service could not be reached. Please try again.";
        public override bool CanBack => true;
        public override bool CanCache => true;
        public override UILayer Layer => UILayer.Overlay;
        [SerializeField] private string connectionAvailableMessage = "Connection available. Press Retry to continue.";
        [SerializeField] private string connectionUnavailableMessage = "Connection is still unavailable. Check settings and retry.";
        private bool canRetry;
        public event Action<ConnectionRecoveryDecision> DecisionRequested;
        private void OnEnable()
        {
            retryButton.onClick.AddListener(Retry); settingsButton.onClick.AddListener(Settings); cancelButton.onClick.AddListener(Cancel);
        }
        protected override void OnDisable()
        {
            retryButton.onClick.RemoveListener(Retry); settingsButton.onClick.RemoveListener(Settings); cancelButton.onClick.RemoveListener(Cancel);
            base.OnDisable();
        }
        private void Retry() => DecisionRequested?.Invoke(ConnectionRecoveryDecision.Retry);
        private void Settings() => DecisionRequested?.Invoke(ConnectionRecoveryDecision.OpenSettings);
        public void RequestBack() => Cancel();
        private void Cancel() => DecisionRequested?.Invoke(ConnectionRecoveryDecision.Cancel);
        public void Render(ConnectionRetryState state, bool settingsSupported, string buttonLabel)
        {
            var prompt = state.Prompt;
            canRetry = prompt.CanRetry;
            titleText.text = offlineTitle;
            messageText.text = !canRetry ? exhaustedMessage : prompt.Message switch
            {
                "settings_opened" => settingsOpenedMessage,
                "settings_failed" => settingsFailedMessage,
                _ => prompt.Status switch
                {
                    ConnectivityStatus.TimedOut => timeoutMessage,
                    ConnectivityStatus.EndpointUnavailable => serviceMessage,
                    _ => retryMessage
                }
            };
            settingsButton.gameObject.SetActive(settingsSupported);
            settingsLabel.text = buttonLabel ?? "Settings";
            if (state.ConnectionAvailable.HasValue)
                messageText.text = state.ConnectionAvailable.Value ? connectionAvailableMessage : connectionUnavailableMessage;
            SetBusy(!state.IsWaiting);
        }
        private void SetBusy(bool busy)
        {
            if (this == null || progress == null) return;
            retryButton.interactable = !busy && canRetry;
            settingsButton.interactable = !busy;
            // Cancellation remains available while checking. The operation owner decides cancellation semantics.
            cancelButton.interactable = true;
            progress.SetActive(busy);
        }
    }
}
