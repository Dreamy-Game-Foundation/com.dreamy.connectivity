using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public sealed class SystemConnectivityTiming : IConnectivityTiming
    {
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        public double Seconds => clock.Elapsed.TotalSeconds;
        public async UniTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            await System.Threading.Tasks.Task.Delay(delay, cancellationToken);
    }
}
