using Codemap.Core;

namespace Codemap.Tests;

public sealed class CodePackerTests
{
    [Fact]
    public async Task PackAsync_UsesMarkdownByDefault()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions { RootDirectory = fixture.Path });

        result.Content.ShouldStartWith("# Codemap");
    }

    [Fact]
    public async Task PackAsync_ExcludesDefaultIgnoredDirectoriesAndAppliesPatterns()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "keep.cs"), "class Keep {}\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "bin"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "bin", "ignored.cs"), "class Ignored {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "notes.txt"), "notes\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            IncludePatterns = ["**/*.cs"]
        });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "keep.cs" });
    }

    [Fact]
    public async Task PackAsync_RendersMarkdownAndTransformsContent()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "// comment\nclass Sample {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            Format = OutputFormat.Markdown,
            ShowLineNumbers = true,
            RemoveComments = true,
            RemoveEmptyLines = true
        });

        result.Content.ShouldContain("## sample.cs");
        result.Content.ShouldContain("   1: class Sample {}");
        result.Content.ShouldNotContain("comment");
    }

    [Fact]
    public async Task PackAsync_EnforcesTokenBudget()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.txt"), "This output is deliberately larger than its budget.");

        await Should.ThrowAsync<InvalidOperationException>(() => new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            TokenBudget = 1
        }));
    }

    [Fact]
    public async Task PackAsync_SkipsBinaryFiles()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(fixture.Path, "image.bin"), [0, 1, 2, 3]);
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "source.cs"), "class Source {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions { RootDirectory = fixture.Path });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "source.cs" });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"codemap-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }
        public string Path { get; } = string.Empty;

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
