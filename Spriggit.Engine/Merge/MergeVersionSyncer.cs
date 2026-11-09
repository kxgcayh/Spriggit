using LibGit2Sharp;
using Noggog;
using Noggog.IO;
using NuGet.Versioning;
using Spriggit.Core;
using Spriggit.Core.Services.Singletons;
using Spriggit.Engine.Services.Singletons;

namespace Spriggit.Engine.Merge;

public class MergeVersionSyncer
{
    private readonly IEntryPointCache _entryPointCache;
    private readonly SpriggitExternalMetaPersister _externalMetaPersister;
    private readonly SpriggitFileLocator _spriggitFileLocator;
    private readonly GetMetaToUse _getMetaToUse;
    private readonly GitFolderLocator _gitFolderLocator;
    
    public MergeVersionSyncer(
        GetMetaToUse getMetaToUse,
        IEntryPointCache entryPointCache,
        SpriggitExternalMetaPersister externalMetaPersister,
        SpriggitFileLocator spriggitFileLocator,
        GitFolderLocator gitFolderLocator)
    {
        _getMetaToUse = getMetaToUse;
        _entryPointCache = entryPointCache;
        _externalMetaPersister = externalMetaPersister;
        _spriggitFileLocator = spriggitFileLocator;
        _gitFolderLocator = gitFolderLocator;
    }

    public async Task DetectAndFix(
        DirectoryPath spriggitModPath,
        DirectoryPath? dataFolder)
    {
        var gitRootPath = _gitFolderLocator.Get(spriggitModPath);
        
        using var repo = GitMergeHelper.Open(gitRootPath);
        var parents = GitMergeHelper.GetMergeParents(repo);

        var lhsSha = parents[0].Sha;
        var rhsSha = parents[1].Sha;

        var lhsMeta = GetEmbeddedMeta(repo, lhsSha, spriggitModPath);
        var rhsMeta = GetEmbeddedMeta(repo, rhsSha, spriggitModPath);
        if (lhsMeta.Release != rhsMeta.Release)
        {
            throw new ArgumentException($"Releases did not match {lhsMeta.Release} != {rhsMeta.Release}");
        }

        if (lhsMeta.Source.PackageName != rhsMeta.Source.PackageName)
        {
            throw new ArgumentException($"PackageName did not match {lhsMeta.Source.PackageName} != {rhsMeta.Source.PackageName}");
        }

        if (lhsMeta.Source.Version == rhsMeta.Source.Version)
        {
            // Nothing to fix
            return;
        }

        if (!NuGetVersion.TryParse(lhsMeta.Source.Version, out var lhsVersion))
        {
            throw new ArgumentException($"Could not convert to nuget version {lhsMeta.Source.Version}");
        }
        if (!NuGetVersion.TryParse(rhsMeta.Source.Version, out var rhsVersion))
        {
            throw new ArgumentException($"Could not convert to nuget version {rhsMeta.Source.Version}");
        }

        var minVersion = new NuGetVersion(0, 20, 0);
        if (lhsVersion < minVersion || rhsVersion < minVersion)
        {
            throw new ArgumentException($"Minimum spriggit version compatible with this feature is {minVersion}");
        }

        string oldSha, newSha;
        if (lhsVersion < rhsVersion)
        {
            oldSha = lhsSha;
            newSha = rhsSha;
        }
        else
        {
            oldSha = rhsSha;
            newSha = lhsSha;
        }
        
        await ExecuteUpgrade(
            repo: repo, 
            oldSha: oldSha,
            newSha: newSha,
            spriggitPath: spriggitModPath,
            dataFolder: dataFolder);
    }

    private SpriggitModKeyMeta GetEmbeddedMeta(
        Repository repo,
        string sha,
        DirectoryPath spriggitPath)
    {
        var origBranch = repo.Head;
        var branch = GitMergeHelper.CreateTempBranch(repo, $"Spriggit-Merge-Fix-{origBranch.Tip.Sha.Substring(0, 6)}", repo.Lookup<Commit>(sha));
        try
        {
            Commands.Checkout(repo, branch);
            return _externalMetaPersister.TryParseEmbeddedMeta(spriggitPath)
                   ?? throw new ArgumentException($"Could not find spriggit meta for sha {sha}");
        }
        finally
        {
            GitMergeHelper.Restore(repo, origBranch, branch);
        }
    }

    private async Task ExecuteUpgrade(
        Repository repo,
        string oldSha,
        string newSha,
        DirectoryPath spriggitPath,
        DirectoryPath? dataFolder)
    {
        var origBranch = repo.Head;
        var shaPrefix = origBranch.Tip.Sha.Substring(0, 6);
        using var tmp = TempFolder.Factory();
        Branch? newBranch = null, oldBranch = null;
        SpriggitModKeyMeta newExternalMeta, oldExternalMeta;
        IEngineEntryPoint oldEntryPoint, newEntryPoint;
        string modPath;
        KnownMaster[] knownMasters;

        try
        {
            newBranch = GitMergeHelper.CreateTempBranch(repo, $"Spriggit-Merge-Fix-New-{shaPrefix}", repo.Lookup<Commit>(newSha));
            Commands.Checkout(repo, newBranch);

            var newMeta = await _getMetaToUse.Get(null, spriggitPath, CancellationToken.None);
            newEntryPoint = await _entryPointCache.GetFor(newMeta.ToMeta(), CancellationToken.None)
                            ?? throw new NullReferenceException($"Could not construct entry point for {newMeta}");

            newExternalMeta = _externalMetaPersister.TryParseEmbeddedMeta(spriggitPath)
                              ?? throw new NullReferenceException($"Could not find embedded meta on {newSha}");

            oldBranch = GitMergeHelper.CreateTempBranch(repo, $"Spriggit-Merge-Fix-Old-{shaPrefix}", repo.Lookup<Commit>(oldSha));
            Commands.Checkout(repo, oldBranch);

            var oldMeta = await _getMetaToUse.Get(null, spriggitPath, CancellationToken.None);
            oldEntryPoint = await _entryPointCache.GetFor(oldMeta.ToMeta(), CancellationToken.None)
                            ?? throw new NullReferenceException($"Could not construct entry point for {oldMeta}");

            oldExternalMeta = _externalMetaPersister.TryParseEmbeddedMeta(spriggitPath)
                              ?? throw new NullReferenceException($"Could not find embedded meta on {oldSha}");

            if (newExternalMeta.Release != oldExternalMeta.Release)
            {
                throw new ArgumentException($"External meta releases did not match {newExternalMeta.Release} != {oldExternalMeta.Release}");
            }

            if (newExternalMeta.ModKey != oldExternalMeta.ModKey)
            {
                throw new ArgumentException($"External meta ModKeys did not match {newExternalMeta.ModKey} != {oldExternalMeta.ModKey}");
            }

            modPath = Path.Combine(tmp.Dir.Path, oldExternalMeta.ModKey.FileName);
            knownMasters = _spriggitFileLocator.LocateAndParse(spriggitPath)?.KnownMasters ?? [];

            await oldEntryPoint.Deserialize(
                inputPath: spriggitPath, outputPath: modPath, dataPath: dataFolder,
                knownMasters: knownMasters,
                workDropoff: null, fileSystem: null, streamCreator: null, cancel: CancellationToken.None);
        }
        finally
        {
            // Back on the merge commit, so the new-format files below land in its working tree
            GitMergeHelper.Restore(repo, origBranch, newBranch, oldBranch);
        }

        await newEntryPoint.Serialize(modPath, spriggitPath, dataFolder,
            knownMasters: knownMasters,
            newExternalMeta.Release,
            null, null, null, newExternalMeta.Source, throwIfUnknown: true, cancel: CancellationToken.None);
    }
}
