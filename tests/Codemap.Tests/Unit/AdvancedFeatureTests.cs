using Codemap.Core;

namespace Codemap.Tests;

public sealed class AdvancedFeatureTests
{
    [Fact]
    public async Task PackAsync_SecurityCheckRunsDevSkim()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "settings.cs"), "var setting = \"ordinary-value\";\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            EnableSecurityCheck = true
        });

        result.Files.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PackAsync_RedactsNonSecretDevSkimFindingsWithoutExcludingFile()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "unsafe.cs"), "var hash = new MD5CryptoServiceProvider();\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "safe.cs"), "class Safe {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            EnableSecurityCheck = true
        });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "safe.cs", "unsafe.cs" });
        result.Files.Single(file => file.RelativePath == "unsafe.cs").Content.ShouldContain("***");
        result.Files.Single(file => file.RelativePath == "unsafe.cs").Content.ShouldNotContain("MD5CryptoServiceProvider");
        result.ExcludedFiles.ShouldBeEmpty();
    }

    [Fact]
    public async Task PackAsync_RedactsDevSkimTokenFindingsWithoutExcludingFile()
    {
        using var fixture = new TemporaryDirectory();
        const string secret = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "settings.cs"), $"// offset before secret\nvar key = \"{secret}\";\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            EnableSecurityCheck = true,
            RemoveComments = true
        });

        result.Files.ShouldHaveSingleItem();
        result.Files[0].Content.ShouldContain("***");
        result.Files[0].Content.ShouldNotContain(secret);
        result.Files[0].Content.ShouldNotContain("offset before secret");
        result.ExcludedFiles.ShouldBeEmpty();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"codemap-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
