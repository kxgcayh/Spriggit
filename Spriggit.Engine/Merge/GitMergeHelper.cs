using LibGit2Sharp;
using Noggog;

namespace Spriggit.Engine.Merge;

public static class GitMergeHelper
{
    public static Repository Open(DirectoryPath? gitRootPath)
    {
        if (gitRootPath == null)
        {
            throw new Exception("No git repository detected");
        }

        return new Repository(gitRootPath.Value.Path);
    }

    /// Checks the repo is ready for a merge fix (clean tracked files, HEAD is a merge commit) and returns the two parents.
    public static Commit[] GetMergeParents(Repository repo)
    {
        if (repo.Head.Tip == null)
        {
            throw new Exception("Git repository had no commits at HEAD");
        }

        if (repo.RetrieveStatus(new StatusOptions { IncludeUntracked = false }).IsDirty)
        {
            throw new Exception("Git repository has uncommitted changes. Commit or stash them first");
        }

        var parents = repo.Head.Tip.Parents.ToArray();
        if (parents.Length != 2)
        {
            throw new Exception("Git did not have a merge commit at HEAD");
        }

        return parents;
    }

    /// Creates a branch, replacing a leftover one with the same name from a crashed run.
    public static Branch CreateTempBranch(Repository repo, string name, Commit target)
    {
        if (repo.Branches[name] != null)
        {
            repo.Branches.Remove(name);
        }

        return repo.CreateBranch(name, target);
    }

    /// Returns to the original branch and deletes the temp branches. Forced, as anything left on a temp branch is throwaway.
    public static void Restore(Repository repo, Branch origBranch, params Branch?[] tempBranches)
    {
        Commands.Checkout(repo, origBranch, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        foreach (var temp in tempBranches)
        {
            if (temp != null && repo.Branches[temp.FriendlyName] != null)
            {
                repo.Branches.Remove(temp);
            }
        }
    }
}
