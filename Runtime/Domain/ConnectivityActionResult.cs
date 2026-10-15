using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public enum ConnectivityActionStatus { Succeeded, Cancelled, RetryExhausted, Failed, OutcomeUnknown, Busy }
    public readonly struct ConnectivityActionResult
    {
        public ConnectivityActionResult(ConnectivityActionStatus status, int attempts = 0, string errorCode = null)
        { Status = status; Attempts = attempts; ErrorCode = errorCode; }
        public ConnectivityActionStatus Status { get; }
        public int Attempts { get; }
        public string ErrorCode { get; }
        public bool IsSuccess => Status == ConnectivityActionStatus.Succeeded;
    }
}
