using System.ClientModel;
using Codemap.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using OpenAI;
using OpenAI.Chat;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Codemap.Tests;

public sealed class PatchSkillEvaluationTests
{
    [Fact]
    public async Task PatchSkill_PassesHardLiveIntegrationScenarios()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("Live model evaluation is opt-in locally and does not run in standard CI.");
        }

        var settings = LiveEvaluationSettings.Load();
        if (!settings.IsEnabled)
        {
            Assert.Skip("Set CODEMAP_RUN_LIVE_EVAL=true and configure .env.test to run live evaluation.");
        }

        using var chatClient = CreateChatClient(settings);
        var scenarios = new[]
        {
            new HardScenario(
                "Modify existing source and add a focused test.",
                "Add a public static string Version property returning \"1.0.0\" to src/Example.cs and add a focused test in tests/ExampleTests.cs. Run the focused test.",
                "## Files\n\n- `src/Example.cs`\n\n## src/Example.cs\n\n```csharp\npublic sealed class Example {}\n```",
                response => response.Contains("src/Example.cs", StringComparison.Ordinal)
                    && response.Contains("tests/ExampleTests.cs", StringComparison.Ordinal)
                    && response.Contains("Version", StringComparison.Ordinal)
                    && response.Contains("1.0.0", StringComparison.Ordinal)
                    && response.Contains("@@", StringComparison.Ordinal)),
            new HardScenario(
                "Create nested implementation and test directories.",
                "Add a GreetingService class under src/Features/Greetings/GreetingService.cs with a Hello method returning \"Hello\", add tests/Features/Greetings/GreetingServiceTests.cs, and run the narrowest relevant test.",
                "## Files\n\n- `src/Program.cs`\n- `tests/Tests.csproj`\n\n## src/Program.cs\n\n```csharp\npublic static class Program {}\n```\n\n## tests/Tests.csproj\n\n```xml\n<Project Sdk=\"Microsoft.NET.Sdk\" />\n```",
                response => response.Contains("diff --git", StringComparison.Ordinal)
                    && response.Contains("src/Features/Greetings/GreetingService.cs", StringComparison.Ordinal)
                    && response.Contains("tests/Features/Greetings/GreetingServiceTests.cs", StringComparison.Ordinal)
                    && response.Contains("Hello", StringComparison.Ordinal)
                    && response.Contains("new file mode", StringComparison.Ordinal)),
            new HardScenario(
                "Delete obsolete file and append a changelog entry.",
                "Delete obsolete src/Legacy.cs and append one entry to CHANGELOG.md describing removal of the legacy implementation. Do not modify unrelated files. Run the relevant validation.",
                "## Files\n\n- `src/Legacy.cs`\n- `CHANGELOG.md`\n- `src/Keep.cs`\n\n## src/Legacy.cs\n\n```csharp\npublic sealed class Legacy {}\n```\n\n## CHANGELOG.md\n\n```markdown\n# Changelog\n\n## Unreleased\n```\n\n## src/Keep.cs\n\n```csharp\npublic sealed class Keep {}\n```",
                response => response.Contains("deleted file mode", StringComparison.Ordinal)
                    && response.Contains("src/Legacy.cs", StringComparison.Ordinal)
                    && response.Contains("CHANGELOG.md", StringComparison.Ordinal)
                    && response.Contains("diff --git", StringComparison.Ordinal))
        };

        foreach (var scenario in scenarios)
        {
            var context = PatchContext.Wrap(scenario.Context, OutputFormat.Markdown);
            var messages = new[]
            {
                new AiChatMessage(ChatRole.System, "Follow patch-generation instructions exactly. Return only one unified Git diff."),
                new AiChatMessage(ChatRole.User, $"Task: {scenario.Task}\n\nRepository context:\n{context}")
            };

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);
            var result = Evaluate(response.Text, scenario);
            var metric = result.Metrics["LivePatchContract"].ShouldBeOfType<BooleanMetric>();
            metric.Value.ShouldBe(true, $"{scenario.Name}: {metric.Reason}");
        }
    }

    private static IChatClient CreateChatClient(LiveEvaluationSettings settings)
    {
        var options = new OpenAIClientOptions { Endpoint = new Uri(settings.BaseUrl) };
        return new ChatClient(settings.Model, new ApiKeyCredential(settings.ApiKey), options).AsIChatClient();
    }

    private static EvaluationResult Evaluate(string response, HardScenario scenario)
    {
        var checks = new Dictionary<string, Func<bool>>
        {
            ["diff_header"] = () => response.TrimStart().StartsWith("diff --git a/", StringComparison.Ordinal),
            ["file_headers"] = () => response.Contains("--- ", StringComparison.Ordinal) && response.Contains("+++ ", StringComparison.Ordinal),
            ["relative_paths"] = () => !response.Contains("/home/", StringComparison.Ordinal) && !response.Contains("C:\\", StringComparison.Ordinal),
            ["no_markdown_fence"] = () => !response.TrimStart().StartsWith("```", StringComparison.Ordinal),
            ["no_shell_commands"] = () => !response.Contains("#!/bin/bash", StringComparison.Ordinal),
            ["scenario_contract"] = () => scenario.Contract(response)
        };

        var failures = checks.Where(check => !check.Value()).Select(check => check.Key).ToArray();
        var passed = failures.Length == 0;
        var reason = passed ? $"Live model passed scenario: {scenario.Name}." : $"Failed checks: {string.Join(", ", failures)}.";
        return new EvaluationResult(new BooleanMetric("LivePatchContract", passed, reason));
    }

    private sealed record HardScenario(string Name, string Task, string Context, Func<string, bool> Contract);

    private sealed record LiveEvaluationSettings(string ApiKey, string BaseUrl, string Model, bool IsEnabled)
    {
        public static LiveEvaluationSettings Load()
        {
            LoadDotEnv();
            var apiKey = Environment.GetEnvironmentVariable("CODEMAP_OPENAI_API_KEY")
                ?? Environment.GetEnvironmentVariable("API_KEY")
                ?? string.Empty;
            var baseUrl = Environment.GetEnvironmentVariable("CODEMAP_OPENAI_BASE_URL") ?? "https://api.deepseek.com/v1";
            var model = Environment.GetEnvironmentVariable("CODEMAP_OPENAI_MODEL") ?? "deepseek-flash";
            var enabled = string.Equals(Environment.GetEnvironmentVariable("CODEMAP_RUN_LIVE_EVAL"), "true", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(apiKey)
                && !apiKey.Equals("replace-me", StringComparison.OrdinalIgnoreCase);
            return new LiveEvaluationSettings(apiKey, baseUrl, model, enabled);
        }

        private static void LoadDotEnv()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var path = Path.Combine(root, ".env.test");
            if (!File.Exists(path)) return;

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                var separator = trimmed.IndexOf('=');
                if (separator <= 0) continue;
                var name = trimmed[..separator].Trim();
                var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(name, value);
            }
        }

        private static string FindRepositoryRoot(string start)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Codemap.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
