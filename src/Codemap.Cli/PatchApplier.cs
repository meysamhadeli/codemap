using System.Diagnostics;

namespace Codemap.Cli;

internal static class PatchApplier
{
    public static async Task<int> ApplyAsync(
        string patchPath,
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var fullPatchPath = Path.GetFullPath(patchPath, repositoryRoot);
        if (!File.Exists(fullPatchPath))
        {
            Console.Error.WriteLine($"codemap: patch file not found: {patchPath}");
            return 1;
        }

        var patch = await File.ReadAllTextAsync(fullPatchPath, cancellationToken);
        var changedFiles = ExtractChangedFiles(patch);
        if (changedFiles.Count == 0)
        {
            Console.Error.WriteLine("codemap: no file changes found in unified diff.");
            return 1;
        }

        Console.WriteLine("Git patch preview:");
        Console.WriteLine(patch);

        var check = await RunGitAsync(repositoryRoot, ["apply", "--check", "--whitespace=error-all", fullPatchPath], cancellationToken);
        if (check.ExitCode != 0)
        {
            Console.Error.WriteLine("codemap: git apply --check failed.");
            Console.Error.Write(check.Error);
            return check.ExitCode;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("codemap: approval required for each file; run patch from an interactive terminal.");
            return 1;
        }

        foreach (var changedFile in changedFiles)
        {
            Console.Write($"Apply changes to '{changedFile}'? [y/N] ");
            if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Patch cancelled. No changes applied.");
                return 1;
            }
        }

        var apply = await RunGitAsync(repositoryRoot, ["apply", "--whitespace=error-all", fullPatchPath], cancellationToken);
        if (apply.ExitCode != 0)
        {
            Console.Error.WriteLine("codemap: git apply failed.");
            Console.Error.Write(apply.Error);
            return apply.ExitCode;
        }

        Console.WriteLine($"Applied changes to {changedFiles.Count} file(s).");
        return 0;
    }

    private static IReadOnlyList<string> ExtractChangedFiles(string patch)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in patch.Split('\n'))
        {
            if (!line.StartsWith("+++ b/", StringComparison.Ordinal))
            {
                continue;
            }

            var path = line[6..].TrimEnd('\r');
            if (!path.Equals("/dev/null", StringComparison.Ordinal))
            {
                files.Add(path);
            }
        }

        foreach (var line in patch.Split('\n'))
        {
            if (!line.StartsWith("--- a/", StringComparison.Ordinal))
            {
                continue;
            }

            var path = line[6..].TrimEnd('\r');
            if (!path.Equals("/dev/null", StringComparison.Ordinal))
            {
                files.Add(path);
            }
        }

        return files.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static async Task<GitResult> RunGitAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new GitResult(1, string.Empty, "codemap: unable to start git.\n");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new GitResult(process.ExitCode, await outputTask, await errorTask);
    }

    private sealed record GitResult(int ExitCode, string Output, string Error);
}
