using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public interface IConnectivityTiming
    {
        double Seconds { get; }
        UniTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }
}
