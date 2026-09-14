using Codemap.Core;

namespace Codemap.Tests;

public sealed class PackConfigurationTests
{
    [Fact]
    public async Task LoadAsync_ReadsJsonAndApplyToPreservesUnsetDefaults()
    {
        using var fixture = new TemporaryDirectory();
        var path = Path.Combine(fixture.Path, "codemap.json");
        await File.WriteAllTextAsync(path, "{\"format\":\"markdown\",\"removeComments\":true,\"tokenBudget\":100}");

        var configuration = await PackConfiguration.LoadAsync(path);
        var options = configuration.ApplyTo(new PackOptions { RootDirectory = fixture.Path, ShowLineNumbers = true });

        options.Format.ShouldBe(OutputFormat.Markdown);
        options.RemoveComments.ShouldBeTrue();
        options.ShowLineNumbers.ShouldBeTrue();
        options.TokenBudget.ShouldBe(100);
    }

    [Fact]
    public async Task PackAsync_UsesGitignoreAndExcludeFiles()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, ".gitignore"), "*.generated.cs\nprivate/\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, ".ignore"), "notes.txt\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "keep.cs"), "class Keep {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "skip.generated.cs"), "class Skip {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "notes.txt"), "notes\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "private"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "private", "secret.cs"), "class Secret {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions { RootDirectory = fixture.Path });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { ".gitignore", ".ignore", "keep.cs" });
    }

    [Fact]
    public async Task PackAsync_UsesExcludeFilesAndSupportsNegation()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, ".ignore"), "generated/\n!generated/keep.cs\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "generated"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "generated", "keep.cs"), "class Keep {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "generated", "skip.cs"), "class Skip {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions { RootDirectory = fixture.Path });

        result.Files.Select(file => file.RelativePath).ShouldContain("generated/keep.cs");
        result.Files.Select(file => file.RelativePath).ShouldNotContain("generated/skip.cs");
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
