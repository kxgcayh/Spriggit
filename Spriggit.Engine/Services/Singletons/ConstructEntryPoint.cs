using Noggog;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Serilog;

namespace Spriggit.Engine.Services.Singletons;

public class ConstructEntryPoint
{
    private readonly ILogger _logger;
    private readonly ConstructCliEndpoint _constructCliEndpoint;
    private readonly ConstructDotNetToolEndpoint _constructDotNetToolEntryPoint;
    private readonly NuGetVersion _legacyVersion = new(0, 35, 1, 0);

    public ConstructEntryPoint(
        ILogger logger,
        ConstructCliEndpoint constructCliEndpoint,
        ConstructDotNetToolEndpoint constructDotNetToolEntryPoint)
    {
        _logger = logger;
        _constructCliEndpoint = constructCliEndpoint;
        _constructDotNetToolEntryPoint = constructDotNetToolEntryPoint;
    }

    public async Task<IEngineEntryPoint?> ConstructFor(
        DirectoryPath tempPath,
        PackageIdentity ident,
        CancellationToken cancellationToken)
    {
        PackageIdValidator.ThrowIfInvalid(ident.Id);

        // ToDo
        // Don't assume it's a Spriggit package.  Others might have their own versioning
        if (ident.Version <= _legacyVersion)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(
                    $"Translation package {ident} requires a historical Windows-only CLI. "
                    + "Use a package newer than 0.35.1, or upgrade the serialized project on Windows first.");
            }

            return await ConstructForLegacy(tempPath, ident, cancellationToken);
        }
        
        return await _constructDotNetToolEntryPoint.ConstructFor(tempPath, ident, cancellationToken);
    }

    private async Task<IEngineEntryPoint?> ConstructForLegacy(
        DirectoryPath tempPath,
        PackageIdentity ident, 
        CancellationToken cancellationToken)
    {
        var cliEndpoint = await _constructCliEndpoint.ConstructFor(tempPath, ident, cancellationToken);
        if (cliEndpoint == null)
        {
            _logger.Error("Failed to construct cli endpoint for {Ident}", ident);
            return null;
        }
        
        return new EngineEntryPointWrapper(
            _logger,
            ident,
            cliEndpoint,
            cliEndpoint);
    }
}