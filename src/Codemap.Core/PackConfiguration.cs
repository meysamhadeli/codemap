using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codemap.Core;

public sealed class PackConfiguration
{
    public string? OutputPath { get; init; }
    public OutputFormat? Format { get; init; }
    public string[]? IncludePatterns { get; init; }
    public string[]? ExcludePatterns { get; init; }
    public bool? IncludeFileSummary { get; init; }
    public bool? IncludeDirectoryStructure { get; init; }
    public bool? ShowLineNumbers { get; init; }
    public bool? RemoveComments { get; init; }
    public bool? RemoveEmptyLines { get; init; }
    public int? TokenBudget { get; init; }
    public long? MaxFileSizeBytes { get; init; }
    public bool? EnableSecurityCheck { get; init; }
    public bool? IncludeGitDiffs { get; init; }
    public bool? IncludeGitLogs { get; init; }
    public int? GitLogCount { get; init; }
    public int? SplitOutputBytes { get; init; }

    public static async Task<PackConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PackConfiguration>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        }, cancellationToken) ?? new PackConfiguration();
    }

    public PackOptions ApplyTo(PackOptions defaults) => defaults with
    {
        OutputPath = OutputPath ?? defaults.OutputPath,
        Format = Format ?? defaults.Format,
        IncludePatterns = IncludePatterns ?? defaults.IncludePatterns,
        ExcludePatterns = ExcludePatterns ?? defaults.ExcludePatterns,
        IncludeFileSummary = IncludeFileSummary ?? defaults.IncludeFileSummary,
        IncludeDirectoryStructure = IncludeDirectoryStructure ?? defaults.IncludeDirectoryStructure,
        ShowLineNumbers = ShowLineNumbers ?? defaults.ShowLineNumbers,
        RemoveComments = RemoveComments ?? defaults.RemoveComments,
        RemoveEmptyLines = RemoveEmptyLines ?? defaults.RemoveEmptyLines,
        TokenBudget = TokenBudget ?? defaults.TokenBudget,
        MaxFileSizeBytes = MaxFileSizeBytes ?? defaults.MaxFileSizeBytes,
        EnableSecurityCheck = EnableSecurityCheck ?? defaults.EnableSecurityCheck,
        IncludeGitDiffs = IncludeGitDiffs ?? defaults.IncludeGitDiffs,
        IncludeGitLogs = IncludeGitLogs ?? defaults.IncludeGitLogs,
        GitLogCount = GitLogCount ?? defaults.GitLogCount,
        SplitOutputBytes = SplitOutputBytes ?? defaults.SplitOutputBytes
    };
}

public static class ExcludeFileLoader
{
    public static IReadOnlyList<string> Load(string rootDirectory, params string[] fileNames)
    {
        var patterns = new List<string>();
        foreach (var fileName in fileNames)
        {
            var path = Path.Combine(rootDirectory, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            patterns.AddRange(File.ReadLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#')));
        }

        return patterns;
    }
}
