using System;
using System.Threading;
using System.Threading.Tasks;
using WinCompare.Setup.Models;

namespace WinCompare.Setup.Services;

public interface IInstallService
{
    Task InstallAsync(InstallOptions options, IProgress<InstallProgressReport> progress, CancellationToken ct = default);
    Task UninstallAsync(string installDir, bool isPerUser, IProgress<InstallProgressReport> progress, CancellationToken ct = default);
}
