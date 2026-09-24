using System.Diagnostics;

namespace Codemap.Tests;

public sealed class CliIntegrationTests
{
    [Fact]
    public async Task Cli_Help_ReturnsUsage()
    {
        var result = await RunCliAsync("--help");
        var clipboardHelp = await RunCliAsync("clipboard", "--help");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("codemap [options]");
        result.StandardOutput.ShouldContain("codemap config [options]");
        result.StandardOutput.ShouldNotContain("--config-template");
        result.StandardOutput.ShouldContain("--help");
        clipboardHelp.ExitCode.ShouldBe(0);
        clipboardHelp.StandardOutput.ShouldContain("clipboard");
    }

    [Fact]
    public async Task Cli_ApplyOption_PreflightRejectsWithoutInteractiveApproval()
    {
        using var fixture = new TemporaryDirectory();
        var patchPath = Path.Combine(fixture.Path, "changes.patch");
        await File.WriteAllTextAsync(patchPath, "diff --git a/sample.txt b/sample.txt\nnew file mode 100644\nindex 0000000..257cc56\n--- /dev/null\n+++ b/sample.txt\n@@ -0,0 +1 @@\n+applied\n");

        var result = await RunCliInDirectoryAsync(fixture.Path, "--apply", patchPath);

        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldContain("Git patch preview:");
        result.StandardError.ShouldContain("approval required");
        File.Exists(Path.Combine(fixture.Path, "sample.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task Cli_ApplyAlias_RejectsNonDiffInput()
    {
        using var fixture = new TemporaryDirectory();
        var patchPath = Path.Combine(fixture.Path, "changes.patch");
        await File.WriteAllTextAsync(patchPath, "#!/bin/bash\nset -e\necho unsafe\n");

        var result = await RunCliInDirectoryAsync(fixture.Path, "-a", patchPath);

        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("no file changes");
    }

    [Fact]
    public async Task Cli_ShortAliases_WorkForHelpVersionFormatAndOutput()
    {
        var helpResult = await RunCliAsync("-h");
        var versionResult = await RunCliAsync("-v");

        helpResult.ExitCode.ShouldBe(0);
        helpResult.StandardOutput.ShouldContain("--stdout");
        helpResult.StandardOutput.ShouldContain("--clipboard");
        versionResult.ExitCode.ShouldBe(0, versionResult.StandardError);
        Version.TryParse(versionResult.StandardOutput.Trim(), out _).ShouldBeTrue();

        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        var outputPath = Path.Combine(fixture.Path, "result.json");

        var packResult = await RunCliInDirectoryAsync(fixture.Path, "-f", "json", "-o", outputPath, "--output-mode", "file");

        packResult.ExitCode.ShouldBe(0, packResult.StandardError);
        (await File.ReadAllTextAsync(outputPath)).ShouldContain("\"files\"");
    }

    [Fact]
    public async Task Cli_Version_ReturnsAssemblyVersion()
    {
        var result = await RunCliAsync("--version");

        result.ExitCode.ShouldBe(0, result.StandardError);
        Version.TryParse(result.StandardOutput.Trim(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Cli_PacksJsonOutput()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        var outputPath = Path.Combine(fixture.Path, "result.json");

        var result = await RunCliInDirectoryAsync(fixture.Path, "--format", "json", "--output", outputPath, "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("\"files\"");
        output.ShouldContain("sample.cs");
    }

    [Fact]
    public async Task Cli_StdoutCommand_PrintsContentWithoutCreatingOutputFile()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        var outputPath = Path.Combine(fixture.Path, "should-not-exist.md");

        var result = await RunCliInDirectoryAsync(fixture.Path, "--stdout", "-o", outputPath);

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("class Sample {}");
        result.StandardOutput.ShouldNotContain("Packed ");
        File.Exists(outputPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Cli_RepositoryConfig_IsIgnored()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "codemap.json"), "{\"outputMode\":\"stdout\"}");

        var outputPath = Path.Combine(fixture.Path, "configured.md");
        var result = await RunCliInDirectoryAsync(fixture.Path, "--output", outputPath, "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        File.Exists(outputPath).ShouldBeTrue();
        result.StandardOutput.ShouldContain("Packed 2 files");
    }

    [Fact]
    public async Task Cli_PatchMode_PrefixesMarkdownContextWithPatchInstructions()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await RunCliInDirectoryAsync(fixture.Path, "stdout", "-p");

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldStartWith("# Git Patch Generation Instructions");
        result.StandardOutput.ShouldContain("diff --git a/");
        result.StandardOutput.ShouldContain("class Sample {}");
    }

    [Fact]
    public async Task Cli_PatchMode_DefaultsToStandardOutput()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await RunCliInDirectoryAsync(fixture.Path, "-p");

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldStartWith("# Git Patch Generation Instructions");
        result.StandardOutput.ShouldContain("class Sample {}");
        result.StandardError.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cli_SkillOption_LoadsExplicitSkill()
    {
        using var fixture = new TemporaryDirectory();
        var skillPath = Path.Combine(fixture.Path, "review", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(skillPath)!);
        await File.WriteAllTextAsync(skillPath, "# Review rules\n\nCheck tests.");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await RunCliInDirectoryAsync(fixture.Path, "stdout", "--skills", skillPath);

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("## Loaded Skills");
        result.StandardOutput.ShouldContain("# Review rules");
    }

    [Fact]
    public async Task Cli_SkillsOption_LoadsNamedProjectSkill()
    {
        using var fixture = new TemporaryDirectory();
        var skillPath = Path.Combine(fixture.Path, ".agents", "skills", "review", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(skillPath)!);
        await File.WriteAllTextAsync(skillPath, "# Project review rules");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await RunCliInDirectoryAsync(fixture.Path, "stdout", "-s", "review");

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("# Project review rules");
    }

    [Theory]
    [InlineData("json", "\"patchInstructions\"")]
    [InlineData("xml", "<patch_instructions>")]
    [InlineData("plain", "# Git Patch Generation Instructions")]
    public async Task Cli_PatchMode_SupportsAllOutputFormats(string format, string expectedMarker)
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");

        var result = await RunCliInDirectoryAsync(fixture.Path, "stdout", "-p", "--format", format);

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain(expectedMarker);
        result.StandardOutput.ShouldContain("class Sample {}");
    }

    [Fact]
    public async Task Cli_ExcludesMatchingFiles()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "keep.cs"), "class Keep {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "secret.cs"), "class Secret {}\n");
        var outputPath = Path.Combine(fixture.Path, "result.json");

        var result = await RunCliInDirectoryAsync(
            fixture.Path, "--exclude", "secret.cs", "--format", "json", "--output", outputPath, "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("keep.cs");
        output.ShouldNotContain("secret.cs");
    }

    [Fact]
    public async Task Cli_ShortAliases_WorkForIncludeAndExclude()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "keep.cs"), "class Keep {}\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "secret.cs"), "class Secret {}\n");
        var outputPath = Path.Combine(fixture.Path, "result.json");

        var result = await RunCliInDirectoryAsync(
            fixture.Path, "-i", "**/*.cs", "-e", "secret.cs", "-f", "json", "-o", outputPath, "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("keep.cs");
        output.ShouldNotContain("secret.cs");
    }

    [Fact]
    public async Task Cli_IncludeAndExcludeCombineFilesAndFolders()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "README.md"), "readme\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "keep.txt"), "keep\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "skip.txt"), "skip\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "src"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "src", "main.cs"), "class Main {}\n");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "tests"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "tests", "main.tests.cs"), "class Tests {}\n");

        var outputPath = Path.Combine(fixture.Path, "result.json");
        var result = await RunCliInDirectoryAsync(
            fixture.Path,
            "--include", "README.md, src, tests",
            "--exclude", "skip.txt, tests",
            "--format", "json",
            "--output", outputPath,
            "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("README.md");
        output.ShouldContain("src/main.cs");
        output.ShouldNotContain("keep.txt");
        output.ShouldNotContain("skip.txt");
        output.ShouldNotContain("tests/main.tests.cs");
    }

    [Fact]
    public async Task Cli_IncludePatternsTrimWhitespaceAfterComma()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "Program.cs"), "class Program {}");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "Properties"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "Properties", "launchSettings.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "README.md"), "readme");

        var result = await RunCliInDirectoryAsync(
            fixture.Path,
            "--include", "Program.cs, Properties",
            "--format", "json",
            "--output", Path.Combine(fixture.Path, "result.json"),
            "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        var output = await File.ReadAllTextAsync(Path.Combine(fixture.Path, "result.json"));
        output.ShouldContain("Program.cs");
        output.ShouldContain("Properties/launchSettings.json");
        output.ShouldNotContain("README.md");
    }

    [Theory]
    [InlineData("Program.cs ,   Properties")]
    [InlineData("  Program.cs  ,   Properties  ")]
    [InlineData("Program.cs,, Properties")]
    public async Task Cli_IncludePatternsTrimWhitespaceAroundCommaSeparatedValues(string includePatterns)
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "Program.cs"), "class Program {}");
        Directory.CreateDirectory(Path.Combine(fixture.Path, "Properties"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "Properties", "launchSettings.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "README.md"), "readme");
        var outputPath = Path.Combine(fixture.Path, "result.json");

        var result = await RunCliInDirectoryAsync(
            fixture.Path,
            "--include", includePatterns,
            "--format", "json",
            "--output", outputPath,
            "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("Program.cs");
        output.ShouldContain("Properties/launchSettings.json");
        output.ShouldNotContain("README.md");
    }

    [Fact]
    public async Task Cli_ShortAlias_WorksForTokenBudget()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        var outputPath = Path.Combine(fixture.Path, "result.json");

        var result = await RunCliInDirectoryAsync(
            fixture.Path, "-t", "1", "-f", "json", "-o", outputPath, "--output-mode", "file");

        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("token budget");
    }

    [Fact]
    public async Task Cli_SplitsLargeOutput()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.txt"), new string('x', 100));
        var outputPath = Path.Combine(fixture.Path, "result.txt");

        var result = await RunCliInDirectoryAsync(fixture.Path, "--format", "plain", "--output", outputPath, "--output-mode", "file", "--split-output", "10");

        result.ExitCode.ShouldBe(0, result.StandardError);
        File.Exists(outputPath + ".1").ShouldBeTrue();
        Directory.EnumerateFiles(fixture.Path, "result.txt.*").Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task Cli_PacksLocalRemoteRepository()
    {
        using var source = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(source.Path, "remote.cs"), "class Remote {}\n");
        RunGit(source.Path, "-c init.defaultBranch=main init");
        RunGit(source.Path, "config user.name CodemapTests");
        RunGit(source.Path, "config user.email codemap-tests@example.invalid");
        RunGit(source.Path, "add remote.cs");
        RunGit(source.Path, "commit -m initial");

        using var output = new TemporaryDirectory();
        var outputPath = Path.Combine(output.Path, "remote.json");
        var result = await RunCliAsync(
            "--remote", new Uri(source.Path).AbsoluteUri, "--remote-branch", "main",
            "--format", "json", "--output", outputPath, "--output-mode", "file");

        result.ExitCode.ShouldBe(0, result.StandardError);
        (await File.ReadAllTextAsync(outputPath)).ShouldContain("remote.cs");
    }

    [Fact]
    public async Task Cli_WatchModeStartsSuccessfully()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        var outputPath = Path.Combine(fixture.Path, "watch.xml");
        var result = await RunCliUntilOutputAsync(
            fixture.Path, new[] { "--output", outputPath, "--output-mode", "file", "-w" }, "Watching for changes.");

        result.StandardOutput.ShouldContain("Watching for changes.");
    }

    private static async Task<CliResult> RunCliAsync(params string[] arguments)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Codemap.Cli.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(assemblyPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start dotnet.");

        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliResult(process.ExitCode, standardOutput, standardError);
    }

    private static async Task<CliResult> RunCliInDirectoryAsync(string workingDirectory, params string[] arguments)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Codemap.Cli.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        startInfo.ArgumentList.Add(assemblyPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start dotnet.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliResult(process.ExitCode, standardOutput, standardError);
    }

    private static async Task<CliResult> RunCliUntilOutputAsync(string workingDirectory, string[] arguments, string expectedOutput)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "Codemap.Cli.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        startInfo.ArgumentList.Add(assemblyPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start dotnet.");
        var output = new List<string>();
        while (await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) is { } line)
        {
            output.Add(line);
            if (line.Contains(expectedOutput, StringComparison.Ordinal)) break;
        }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        return new CliResult(process.ExitCode, string.Join(Environment.NewLine, output), await process.StandardError.ReadToEndAsync());
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

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

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