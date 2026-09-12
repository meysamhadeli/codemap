namespace Codemap.Core;

public enum OutputFormat
{
    Xml,
    Markdown,
    Plain,
    Json
}

public sealed record PackOptions
{
    public required string RootDirectory { get; init; }
    public string OutputPath { get; init; } = "codemap-output.md";
    public OutputFormat Format { get; init; } = OutputFormat.Markdown;
    public IReadOnlyList<string> IncludePatterns { get; init; } = ["**/*"];
    public IReadOnlyList<string> IgnorePatterns { get; init; } = [];
    public bool IncludeFileSummary { get; init; } = true;
    public bool IncludeDirectoryStructure { get; init; } = true;
    public bool ShowLineNumbers { get; init; }
    public bool RemoveComments { get; init; }
    public bool RemoveEmptyLines { get; init; }
    public int? TokenBudget { get; init; }
    public long? MaxFileSizeBytes { get; init; }
    public bool EnableSecurityCheck { get; init; }
    public bool IncludeGitDiffs { get; init; }
    public bool IncludeGitLogs { get; init; }
    public int GitLogCount { get; init; } = 20;
    public int? SplitOutputBytes { get; init; }
}

public sealed record PackedFile(string RelativePath, string Content, int CharacterCount, int LineCount, int TokenCount = 0);

public sealed record PackResult(
    IReadOnlyList<PackedFile> Files,
    string Content,
    int CharacterCount,
    int EstimatedTokenCount,
    string? GitDiffs = null,
    string? GitLogs = null,
    IReadOnlyList<string>? ExcludedFiles = null)
{
    public int TokenCount => EstimatedTokenCount;
}
