using Microsoft.ApplicationInspector.RulesEngine;
using Microsoft.DevSkim;

namespace Codemap.Core;

public sealed class SecurityScanner
{
    public async Task<IReadOnlyList<string>> ScanAsync(string rootDirectory, IEnumerable<string> relativePaths, CancellationToken cancellationToken = default)
    {
        var paths = relativePaths.ToArray();
        if (paths.Length == 0) return [];

        var processor = new DevSkimRuleProcessor(
            DevSkimRuleSet.GetDefaultRuleSet(),
            new DevSkimRuleProcessorOptions
            {
                SeverityFilter = Severity.Critical | Severity.Important | Severity.Moderate | Severity.BestPractice | Severity.ManualReview,
                ConfidenceFilter = Confidence.High | Confidence.Medium | Confidence.Low,
                EnableSuppressions = true
            });
        var findings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.Combine(rootDirectory, relativePath);
            if (!File.Exists(fullPath)) continue;

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            if (processor.Analyze(content, relativePath).Any(issue => !issue.IsSuppressionInfo)) findings.Add(relativePath);
        }

        return findings.ToArray();
    }
}