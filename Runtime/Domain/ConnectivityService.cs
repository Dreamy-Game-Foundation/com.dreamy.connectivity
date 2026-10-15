using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    /// <summary>Main-thread owned service. Shared probes have independent caller cancellation.</summary>
    public sealed class ConnectivityService : IConnectivityService, IDisposable
    {
        private sealed class Probe
        {
            public readonly CancellationTokenSource Cancellation = new();
            public System.Threading.Tasks.Task<ConnectivityCheckResult> Task;
            public int Waiters;
        }
        private readonly IConnectivityGateway gateway;
        private readonly IConnectivityTiming timing;
        private readonly double positiveTtl, negativeTtl;
        private readonly TimeSpan timeout;
        private readonly System.Collections.Generic.Dictionary<string, Probe> probes = new();
        private readonly System.Collections.Generic.Dictionary<string, (ConnectivityCheckResult result, double expires)> cache = new();
        private readonly object gate = new();
        private bool disposed;
        private int generation;
        public ConnectivityService(IConnectivityGateway gateway, IConnectivityTiming timing = null,
            double positiveTtlSeconds = 3, double negativeTtlSeconds = 1, double timeoutSeconds = 5)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.timing = timing ?? new SystemConnectivityTiming();
            if (double.IsNaN(positiveTtlSeconds) || double.IsInfinity(positiveTtlSeconds) || positiveTtlSeconds < 0 ||
                double.IsNaN(negativeTtlSeconds) || double.IsInfinity(negativeTtlSeconds) || negativeTtlSeconds < 0 ||
                double.IsNaN(timeoutSeconds) || double.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            positiveTtl = positiveTtlSeconds; negativeTtl = negativeTtlSeconds; timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }
        public async UniTask<ConnectivityCheckResult> CheckAsync(ConnectivityCheckRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.TargetId) || !Enum.IsDefined(typeof(ConnectivityRequirement), request.Requirement))
                throw new ArgumentException("A valid target and requirement are required.");
            string key = request.TargetId + ":" + (int)request.Requirement;
            Probe probe;
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(ConnectivityService));
                if (request.Requirement == ConnectivityRequirement.RequestFirst)
                    return new ConnectivityCheckResult(request.TargetId, ConnectivityStatus.Unknown);
                if (!request.ForceRefresh && cache.TryGetValue(key, out var cached) && cached.expires > timing.Seconds)
                    return cached.result.Cached();
                if (!probes.TryGetValue(key, out probe))
                {
                    probe = new Probe();
                    probes.Add(key, probe);
                    probe.Task = RunProbeAsync(key, request, probe, generation);
                }
                probe.Waiters++;
            }
            try
            {
                if (probe.Task.IsCompleted) return probe.Task.GetAwaiter().GetResult();
                return await UniTaskExtensions.AsUniTask<ConnectivityCheckResult>(probe.Task).AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                lock (gate)
                {
                    probe.Waiters--;
                    if (probe.Waiters == 0 && !probe.Task.IsCompleted)
                    {
                        if (probes.TryGetValue(key, out Probe current) && ReferenceEquals(current, probe)) probes.Remove(key);
                        probe.Cancellation.Cancel();
                    }
                }
            }
        }
        private async System.Threading.Tasks.Task<ConnectivityCheckResult> RunProbeAsync(string key,
            ConnectivityCheckRequest request, Probe probe, int version)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(probe.Cancellation.Token);
            deadline.CancelAfter(timeout);
            try
            {
                ConnectivityCheckResult result;
                try { result = await gateway.CheckAsync(request, deadline.Token).AttachExternalCancellation(deadline.Token); }
                catch (OperationCanceledException) when (!probe.Cancellation.IsCancellationRequested)
                { result = new ConnectivityCheckResult(request.TargetId, ConnectivityStatus.TimedOut, "probe_timeout"); }
                if (result.TargetId != request.TargetId) result = new ConnectivityCheckResult(request.TargetId, ConnectivityStatus.Misconfigured, "target_mismatch");
                lock (gate)
                    if (!disposed && generation == version && !probe.Cancellation.IsCancellationRequested)
                        cache[key] = (result, timing.Seconds + (result.Satisfies(request.Requirement) ? positiveTtl : negativeTtl));
                return result;
            }
            finally
            {
                lock (gate)
                {
                    if (probes.TryGetValue(key, out Probe current) && ReferenceEquals(current, probe)) probes.Remove(key);
                    probe.Cancellation.Dispose();
                }
            }
        }
        public void Invalidate() { lock (gate) { generation++; cache.Clear(); } }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true; Invalidate();
                var pending = new System.Collections.Generic.List<Probe>(probes.Values);
                probes.Clear();
                foreach (Probe probe in pending) if (!probe.Task.IsCompleted) probe.Cancellation.Cancel();
            }
        }
    }
}
