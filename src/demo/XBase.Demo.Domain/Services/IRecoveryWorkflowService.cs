using System.Threading;
using System.Threading.Tasks;
using XBase.Demo.Domain.Recovery;

namespace XBase.Demo.Domain.Services;

/// <summary>
/// Provides crash simulation, recovery replay, and support package export workflows.
/// </summary>
public interface IRecoveryWorkflowService
{
  Task<CrashSimulationResult> SimulateCrashAsync(CrashSimulationRequest request, CancellationToken cancellationToken = default);

  Task<RecoveryReplayResult> ReplayRecoveryAsync(RecoveryReplayRequest request, CancellationToken cancellationToken = default);

  Task<SupportPackageResult> CreateSupportPackageAsync(SupportPackageRequest request, CancellationToken cancellationToken = default);
}
