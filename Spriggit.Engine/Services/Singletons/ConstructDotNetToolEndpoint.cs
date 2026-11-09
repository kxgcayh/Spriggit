using System.Diagnostics;
using System.IO.Abstractions;
using Noggog;
using Noggog.Processes.DI;
using NuGet.Packaging.Core;
using Serilog;

namespace Spriggit.Engine.Services.Singletons;

public class ConstructDotNetToolEndpoint
{
    private readonly ILogger _logger;
    private readonly IFileSystem _fileSystem;
    private readonly ProcessFactory _processFactory;
    private readonly DebugState _debugState;
    private readonly DotNetToolTranslationPackagePathProvider _pathProvider;
    private readonly SpriggitTempSourcesProvider _tempSourcesProvider;

    public ConstructDotNetToolEndpoint(
        ILogger logger,
        IFileSystem fileSystem,
        ProcessFactory processFactory,
        DebugState debugState,
        DotNetToolTranslationPackagePathProvider pathProvider,
        SpriggitTempSourcesProvider tempSourcesProvider)
    {
        _logger = logger;
        _fileSystem = fileSystem;
        _processFactory = processFactory;
        _debugState = debugState;
        _pathProvider = pathProvider;
        _tempSourcesProvider = tempSourcesProvider;
    }

    // dotnet tool install --tool-path lays out .store/<id lowercase>/<version>/
    private bool IsToolInstalled(PackageIdentity ident, DirectoryPath toolsPath)
    {
        var storeDir = Path.Combine(toolsPath, ".store", ident.Id.ToLowerInvariant(), ident.Version.ToNormalizedString().ToLowerInvariant());
        var exe = Path.Combine(toolsPath, $"{ident.Id}{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}");
        return _fileSystem.Directory.Exists(storeDir) && _fileSystem.File.Exists(exe);
    }

    private async Task<IDisposable> AcquireLock(string lockPath, CancellationToken cancellationToken)
    {
        _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        while (true)
        {
            try
            {
                return _fileSystem.FileStream.New(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                await Task.Delay(250, cancellationToken);
            }
        }
    }

    public async Task<DotNetToolEntryPoint?> ConstructFor(
        DirectoryPath tempPath,
        PackageIdentity ident,
        CancellationToken cancellationToken)
    {
        DirectoryPath toolsPath = _pathProvider.Path(tempPath, ident);

        if (_debugState.ClearNugetSources)
        {
            _logger.Information("In debug mode.  Forcing dotnet tool reinstall");
        }
        
        if (_debugState.ClearNugetSources || !IsToolInstalled(ident, toolsPath))
        {
            // Another Spriggit process may be installing the same tool right now
            using var installLock = await AcquireLock($"{toolsPath.Path}.lock", cancellationToken);
            if (!_debugState.ClearNugetSources && IsToolInstalled(ident, toolsPath))
            {
                return new DotNetToolEntryPoint(ident, _logger.ForContext<DotNetToolEntryPoint>(), _processFactory, _pathProvider, _tempSourcesProvider);
            }
            try
            {
                _logger.Information("Running DotNet Entry point install.  Clearing old path {ToolsPath}", toolsPath);
                _fileSystem.Directory.DeleteEntireFolder(toolsPath);
                var args = $"tool install {ident.Id} --version {ident.Version} --tool-path \"{toolsPath}\"";
                _logger.Information("Running DotNet Entry point install with Args: {Args}", args);
                using var processWrapper = _processFactory.Create(
                    new ProcessStartInfo("dotnet")
                    {
                        Arguments = args,
                    },
                    cancel: cancellationToken,
                    killWithParent: false);
                using var outputSub = processWrapper.Output
                    .Subscribe(x =>
                    {
                        _logger.Information(x);
                    });
                using var err = processWrapper.Error
                    .Subscribe(x =>
                    {
                        _logger.Error(x);
                    });
                var ret = await processWrapper.Run();
                if (ret != 0)
                {
                    _logger.Error("Error running dotnet tool install for {Identity}", ident);
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error preparing dotnet tool folder for {Identity}", ident);
                return null;
            }
        }

        return new DotNetToolEntryPoint(
            ident,
            _logger.ForContext<DotNetToolEntryPoint>(),
            _processFactory,
            _pathProvider,
            _tempSourcesProvider);
    }
}