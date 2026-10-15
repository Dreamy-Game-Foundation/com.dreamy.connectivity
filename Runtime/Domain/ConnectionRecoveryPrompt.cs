using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public enum ConnectionRecoveryDecision { Cancel, Retry, OpenSettings }
    public enum OpenConnectionSettingsStatus { Opened, Unsupported, Failed }
    public readonly struct ConnectionRecoveryPrompt
    {
        public ConnectionRecoveryPrompt(string operationId, ConnectivityStatus status, bool canRetry, int attempts, string message = null)
        { OperationId = operationId; Status = status; CanRetry = canRetry; Attempts = attempts; Message = message; }
        public string OperationId { get; }
        public ConnectivityStatus Status { get; }
        public bool CanRetry { get; }
        public int Attempts { get; }
        public string Message { get; }
    }
}
