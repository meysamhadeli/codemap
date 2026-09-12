using System.Diagnostics;
using Codemap.Core;

namespace Codemap.Tests;

public sealed class ExtendedFeatureTests
{
    [Fact]
    public async Task PackAsync_RendersEveryOutputFormat()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        foreach (var format in Enum.GetValues<OutputFormat>())
        {
            var result = await new CodePacker().PackAsync(new PackOptions
            {
                RootDirectory = fixture.Path,
                Format = format
            });

            result.Content.ShouldContain("sample.cs");
            result.TokenCount.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public async Task PackAsync_CanOmitSummaryAndDirectoryStructure()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            Format = OutputFormat.Xml,
            IncludeFileSummary = false,
            IncludeDirectoryStructure = false
        });

        result.Content.ShouldNotContain("file_summary");
        result.Content.ShouldNotContain("directory_structure");
        result.Content.ShouldContain("<files>");
    }

    [Fact]
    public async Task PackAsync_ReportsFileAndOutputTokenMetadata()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions { RootDirectory = fixture.Path });

        result.Files[0].TokenCount.ShouldBeGreaterThan(0);
        result.TokenCount.ShouldBeGreaterThan(result.Files.Sum(file => file.TokenCount));
        result.CharacterCount.ShouldBe(result.Content.Length);
    }

    [Fact]
    public async Task PackAsync_SkipsInvalidUtf8Files()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllBytesAsync(Path.Combine(fixture.Path, "invalid.txt"), [0xC3, 0x28]);
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "valid.txt"), "valid\n");

        var result = await new CodePacker().PackAsync(new PackOptions { RootDirectory = fixture.Path });

        result.Files.Select(file => file.RelativePath).ShouldBe(new[] { "valid.txt" });
    }

    [Fact]
    public async Task PackAsync_ThrowsWhenRootDoesNotExist()
    {
        using var fixture = new TemporaryDirectory();
        var missingRoot = Path.Combine(fixture.Path, "missing");

        await Should.ThrowAsync<DirectoryNotFoundException>(() => new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = missingRoot
        }));
    }

    [Fact]
    public async Task PackAsync_HonorsCancellation()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => new CodePacker().PackAsync(
            new PackOptions { RootDirectory = fixture.Path }, cancellation.Token));
    }

    [Fact]
    public async Task PackConfiguration_OverridesOnlyConfiguredValues()
    {
        using var fixture = new TemporaryDirectory();
        var path = Path.Combine(fixture.Path, "codemap.json");
        await File.WriteAllTextAsync(path, "{\"format\":\"json\",\"removeComments\":true}");

        var defaults = new PackOptions
        {
            RootDirectory = fixture.Path,
            Format = OutputFormat.Markdown,
            ShowLineNumbers = true
        };
        var options = (await PackConfiguration.LoadAsync(path)).ApplyTo(defaults);

        options.Format.ShouldBe(OutputFormat.Json);
        options.RemoveComments.ShouldBeTrue();
        options.ShowLineNumbers.ShouldBeTrue();
    }

    [Fact]
    public async Task PackAsync_IncludesGitDiffsAndLogs()
    {
        using var fixture = new TemporaryDirectory();
        var sourcePath = Path.Combine(fixture.Path, "sample.cs");
        await File.WriteAllTextAsync(sourcePath, "class Sample {}\n");
        RunGit(fixture.Path, "init");
        RunGit(fixture.Path, "config user.name CodemapTests");
        RunGit(fixture.Path, "config user.email codemap-tests@example.invalid");
        RunGit(fixture.Path, "add sample.cs");
        RunGit(fixture.Path, "commit -m initial");
        await File.WriteAllTextAsync(sourcePath, "class Changed {}\n");

        var result = await new CodePacker().PackAsync(new PackOptions
        {
            RootDirectory = fixture.Path,
            Format = OutputFormat.Xml,
            IncludeGitDiffs = true,
            IncludeGitLogs = true,
            GitLogCount = 1
        });

        result.GitDiffs.ShouldContain("Changed");
        result.GitLogs.ShouldContain("initial");
        result.Content.ShouldContain("<git_diffs>");
        result.Content.ShouldContain("<git_logs>");
    }

    private static void RunGit(string directory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Unable to start git.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(process.StandardError.ReadToEnd());
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"codemap-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
    }
}
