using Microsoft.ApplicationInspector.RulesEngine;
using Microsoft.DevSkim;

namespace Codemap.Core;

public sealed class SecurityScanner
{
    public async Task<SecurityScanResult> ScanAsync(string rootDirectory, IEnumerable<string> relativePaths, CancellationToken cancellationToken = default)
    {
        var paths = relativePaths.ToArray();
        if (paths.Length == 0) return new SecurityScanResult([], new Dictionary<string, IReadOnlyList<SecurityRedaction>>());

        var processor = new DevSkimRuleProcessor(
            DevSkimRuleSet.GetDefaultRuleSet(),
            new DevSkimRuleProcessorOptions
            {
                SeverityFilter = Severity.Critical | Severity.Important | Severity.Moderate | Severity.BestPractice | Severity.ManualReview,
                ConfidenceFilter = Confidence.High | Confidence.Medium | Confidence.Low,
                EnableSuppressions = true
            });
        var redactions = new Dictionary<string, IReadOnlyList<SecurityRedaction>>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(rootDirectory, relativePath);
            if (!File.Exists(fullPath)) continue;

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var issues = processor.Analyze(content, relativePath)
                .Where(issue => !issue.IsSuppressionInfo)
                .ToArray();
            if (issues.Length == 0) continue;

            var issueRedactions = issues
                .Select(issue => new SecurityRedaction(issue.Boundary.Index, issue.Boundary.Length))
                .Where(redaction => redaction.Length > 0)
                .ToArray();
            if (issueRedactions.Length > 0)
            {
                redactions[relativePath] = issueRedactions;
            }
        }

        return new SecurityScanResult([], redactions);
    }
}

public sealed record SecurityRedaction(int Index, int Length);

public sealed record SecurityScanResult(
    IReadOnlyList<string> ExcludedFiles,
    IReadOnlyDictionary<string, IReadOnlyList<SecurityRedaction>> Redactions);