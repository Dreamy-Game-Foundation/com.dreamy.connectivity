using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dreamy.Connectivity
{
    public interface IConnectionSettingsGateway
    {
        bool IsSupported { get; }
        string ButtonLabel { get; }
        UniTask<OpenConnectionSettingsStatus> OpenAsync(CancellationToken cancellationToken);
    }
}
