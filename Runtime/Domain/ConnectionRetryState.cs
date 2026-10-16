namespace Dreamy.Connectivity
{
    public readonly struct ConnectionRetryState
    {
        public ConnectionRetryState(ConnectionRecoveryPrompt prompt, bool isWaiting, bool? connectionAvailable)
        { Prompt = prompt; IsWaiting = isWaiting; ConnectionAvailable = connectionAvailable; }
        public ConnectionRecoveryPrompt Prompt { get; }
        public bool IsWaiting { get; }
        public bool? ConnectionAvailable { get; }
    }
}
