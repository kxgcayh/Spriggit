using Noggog.GitRepository;
using Serilog;

namespace Spriggit.Engine.Services.Singletons;

public class GitOperations
{
    private readonly ILogger _logger;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly GitFolderLocator _gitFolderLocator;

    public GitOperations(
        ILogger logger,
        IGitRepositoryFactory gitRepositoryFactory,
        GitFolderLocator gitFolderLocator)
    {
        _logger = logger;
        _gitRepositoryFactory = gitRepositoryFactory;
        _gitFolderLocator = gitFolderLocator;
    }

    /// No repository means nothing to protect (false). If the repository exists but cannot be read, we
    /// cannot prove it is clean, so report true and block callers from overwriting files.
    public Task<bool> HasUncommittedChanges(string workingDirectory)
    {
        var gitFolder = _gitFolderLocator.Get(workingDirectory);
        if (gitFolder == null)
        {
            _logger.Warning("No git repository found for directory {WorkingDirectory}", workingDirectory);
            return Task.FromResult(false);
        }

        var repoRoot = Path.GetDirectoryName(gitFolder.Value);
        if (repoRoot == null)
        {
            _logger.Warning("Could not determine git repository root for {GitFolder}", gitFolder);
            return Task.FromResult(false);
        }

        try
        {
            using var repository = _gitRepositoryFactory.Get(repoRoot);
            return Task.FromResult(repository.HasUncommittedChanges);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking for uncommitted changes in {WorkingDirectory}", workingDirectory);
            return Task.FromResult(true);
        }
    }

    /// Commits only the files under <paramref name="workingDirectory"/>, not the rest of the repository.
    public Task<bool> CommitChanges(string workingDirectory, string commitMessage)
    {
        var gitFolder = _gitFolderLocator.Get(workingDirectory);
        if (gitFolder == null)
        {
            _logger.Error("No git repository found for directory {WorkingDirectory}", workingDirectory);
            return Task.FromResult(false);
        }

        var repoRoot = Path.GetDirectoryName(gitFolder.Value);
        if (repoRoot == null)
        {
            _logger.Error("Could not determine git repository root for {GitFolder}", gitFolder);
            return Task.FromResult(false);
        }

        try
        {
            using var repository = _gitRepositoryFactory.Get(repoRoot);

            var relative = Path.GetRelativePath(repoRoot, Path.GetFullPath(workingDirectory))
                .Replace(Path.DirectorySeparatorChar, '/');
            repository.Stage(relative is "." or "" ? "*" : relative + "/*");
            repository.Commit(commitMessage);

            _logger.Information("Successfully committed changes with message: {CommitMessage}", commitMessage);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error committing changes in {WorkingDirectory}", workingDirectory);
            return Task.FromResult(false);
        }
    }
}
