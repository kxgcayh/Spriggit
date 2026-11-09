using Mutagen.Bethesda;
using Mutagen.Bethesda.Skyrim;
using Noggog.IO;
using Shouldly;
using Spriggit.CLI.Lib.Commands.Standardize;
using Xunit;

namespace Spriggit.Tests;

public class StandardizeRunnerTests
{
    [Fact]
    public async Task OverwritesLongerExistingOutput()
    {
        using var tmp = TempFolder.Factory();
        var input = Path.Combine(tmp.Dir, "Test.esp");
        new SkyrimMod(Mutagen.Bethesda.Plugins.ModKey.FromNameAndExtension("Test.esp"), SkyrimRelease.SkyrimSE)
            .WriteToBinary(input);
        var output = Path.Combine(tmp.Dir, "Out.esp");
        File.WriteAllBytes(output, new byte[100_000]);

        await StandardizeRunner.Run(new StandardizeCommand
        {
            InputPath = input,
            OutputPath = output,
            GameRelease = GameRelease.SkyrimSE,
        });

        new FileInfo(output).Length.ShouldBeLessThan(100_000);
    }
}
