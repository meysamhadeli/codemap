using System.Diagnostics;

namespace Codemap.Core;

public sealed class GitMetadata
{
    public async Task<string?> RunAsync(string rootDirectory, string arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = rootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.TrimEnd() : null;
    }
}