using System.Diagnostics;

namespace Codemap.Tests;

public sealed class CliIntegrationTests
{
    [Fact]
    public async Task Cli_Help_ReturnsUsage()
    {
        var result = await RunCliAsync("--help");

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("codemap [options]");
        result.StandardOutput.ShouldContain("--help");
    }

    [Fact]
    public async Task Cli_PacksJsonOutput()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.cs"), "class Sample {}\n");
        var outputPath = Path.Combine(fixture.Path, "result.json");

        var result = await RunCliAsync("--root", fixture.Path, "--format", "json", "--output", outputPath);

        result.ExitCode.ShouldBe(0, result.StandardError);
        var output = await File.ReadAllTextAsync(outputPath);
        output.ShouldContain("\"files\"");
        output.ShouldContain("sample.cs");
    }

    [Fact]
    public async Task Cli_SplitsLargeOutput()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "sample.txt"), new string('x', 100));
        var outputPath = Path.Combine(fixture.Path, "result.txt");

        var result = await RunCliAsync("--root", fixture.Path, "--format", "plain", "--output", outputPath, "--split-output", "10");

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
            "--format", "json", "--output", outputPath);

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
            new[] { "--root", fixture.Path, "--output", outputPath, "--watch" }, "Watching for changes.");

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

    private static async Task<CliResult> RunCliUntilOutputAsync(string[] arguments, string expectedOutput)
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