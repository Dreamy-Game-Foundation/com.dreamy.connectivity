using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public enum ConnectedOperationStatus { Succeeded, ConnectivityFailure, ServiceUnavailable, Rejected, OutcomeUnknown }
    public readonly struct ConnectedOperationResult
    {
        public ConnectedOperationResult(ConnectedOperationStatus status, string errorCode = null) { Status = status; ErrorCode = errorCode; }
        public ConnectedOperationStatus Status { get; }
        public string ErrorCode { get; }
        public static ConnectedOperationResult Success() => new(ConnectedOperationStatus.Succeeded);
    }
}
