using Shouldly;
using Spriggit.Engine.Services.Singletons;
using Xunit;

namespace Spriggit.Tests;

public class PackageIdValidatorTests
{
    [Theory]
    [InlineData("Spriggit.Yaml.Skyrim")]
    [InlineData("Some_Pkg-1.2")]
    public void Valid(string id) => PackageIdValidator.IsValid(id).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("x --add-source http://evil")]
    [InlineData("--version")]
    [InlineData("a\"b")]
    [InlineData("../x")]
    public void Invalid(string id) => PackageIdValidator.IsValid(id).ShouldBeFalse();
}
