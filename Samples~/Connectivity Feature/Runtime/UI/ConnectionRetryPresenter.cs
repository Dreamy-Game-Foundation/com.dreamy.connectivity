using System;
using Dreamy.UI;
namespace Dreamy.Connectivity
{
    /// <summary>One presenter per panel opening, created/disposed by PanelPresenterFactory/host.</summary>
    public sealed class ConnectionRetryPresenter : IPanelPresenter
    {
        private readonly ConnectionRetryModel model;
        private readonly IConnectionRetryView view;
        private readonly bool settingsSupported;
        private readonly string settingsLabel;
        private bool bound;
        public ConnectionRetryPresenter(ConnectionRetryModel model, IConnectionRetryView view,
            bool settingsSupported = false, string settingsLabel = null)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.settingsSupported = settingsSupported; this.settingsLabel = settingsLabel;
        }
        public void Show()
        {
            if (!bound)
            {
                bound = true; model.StateChanged += Render; view.DecisionRequested += Decide;
            }
            Render();
        }
        private void Render() => view.Render(model.State, settingsSupported, settingsLabel);
        private void Decide(ConnectionRecoveryDecision decision) => model.Decide(decision);
        public void Dispose()
        {
            if (!bound) return;
            bound = false; model.StateChanged -= Render; view.DecisionRequested -= Decide;
            model.Cancel();
        }
    }
}
