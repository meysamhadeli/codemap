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
    public async Task PackAsync_ExcludesDefaultDirectoriesAndAppliesPatterns()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "keep.cs"), "class Keep {}\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "bin"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "bin", "excluded.cs"), "class Excluded {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "notes.txt"), "notes\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            IncludePatterns = ["**/*.cs"]
        });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "keep.cs" });
    }

    [Fact]
    public async Task PackAsync_IncludeAndExcludePatternsSupportFilesFoldersAndGlobs()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "root.cs"), "class Root {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "other.txt"), "other\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "src", "nested"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "main.cs"), "class Main {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "nested", "helper.cs"), "class Helper {}\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "generated"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "generated", "generated.cs"), "class Generated {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            IncludePatterns = ["root.cs", "src"],
            ExcludePatterns = ["**/nested/*.cs", "generated"]
        });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "root.cs", "src/main.cs" });
    }

    [Fact]
    public async Task PackAsync_SingleStarMatchesFilesInAnyFolder()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "root.cs"), "class Root {}\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "src"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "nested.cs"), "class Nested {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "nested.txt"), "nested\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            IncludePatterns = ["*.cs"]
        });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "root.cs", "src/nested.cs" });
    }

    [Fact]
    public async Task PackAsync_WildcardFolderPatternsMatchDescendantsAndTrimCommaSpaces()
    {
        using var fixture = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(fixture.Path, "src", "Api"));
        Directory.CreateDirectory(Path.Combine(fixture.Path, "src", "tests"));
        Directory.CreateDirectory(Path.Combine(fixture.Path, "src", "foo"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "Api", "test.cs"), "class ApiTest {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "tests", "test.cs"), "class Tests {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "foo", "keep.cs"), "class Keep {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            IncludePatterns = ["**/Api", "/**/tests/**", " **/foo"],
            ExcludePatterns = [" **/tests", "**/foo/keep.cs"]
        });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "src/Api/test.cs" });
    }

    [Fact]
    public async Task PackAsync_MatchesWildcardFoldersAtAnyDepth()
    {
        using var fixture = new TemporaryDirectory();
        var deepFoo = Path.Combine(fixture.Path, "one", "two", "three", "four", "five", "foo");
        var deepBar = Path.Combine(fixture.Path, "alpha", "beta", "gamma", "bar");
        Directory.CreateDirectory(deepFoo);
        Directory.CreateDirectory(deepBar);
        Directory.CreateDirectory(Path.Combine(fixture.Path, "other"));
        await File.WriteAllTextAsync(Path.Combine(deepFoo, "foo.cs"), "class Foo {}");
        await File.WriteAllTextAsync(Path.Combine(deepBar, "bar.cs"), "class Bar {}");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "other", "other.cs"), "class Other {}");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            IncludePatterns = ["**/foo/**", "**/bar"]
        });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[]
        {
            "alpha/beta/gamma/bar/bar.cs",
            "one/two/three/four/five/foo/foo.cs"
        });
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
