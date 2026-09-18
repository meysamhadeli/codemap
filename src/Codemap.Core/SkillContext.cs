using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Codemap.Core;

public sealed record LoadedSkill(string Name, string SourcePath, string Content);

public static class SkillContext
{
    public static async Task<IReadOnlyList<LoadedSkill>> LoadAsync(
        string rootDirectory,
        IReadOnlyList<string> specifications,
        CancellationToken cancellationToken = default)
    {
        var skills = new List<LoadedSkill>();
        foreach (var specification in specifications.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (IsExplicitPath(specification))
            {
                var fullPath = Path.GetFullPath(specification, Directory.GetCurrentDirectory());
                skills.Add(await ReadAsync(fullPath, Path.GetFileName(Path.GetDirectoryName(fullPath) ?? fullPath), cancellationToken));
                continue;
            }

            var match = GetSearchRoots(rootDirectory)
                .Select(directory => Path.Combine(directory, specification, "SKILL.md"))
                .FirstOrDefault(File.Exists);
            if (match is null) throw new FileNotFoundException($"Skill '{specification}' was not found.");
            skills.Add(await ReadAsync(match, specification, cancellationToken));
        }

        return skills;
    }

    public static string Wrap(string content, OutputFormat format, IReadOnlyList<LoadedSkill> skills)
    {
        if (skills.Count == 0) return content;

        return format switch
        {
            OutputFormat.Json => WrapJson(content, skills),
            OutputFormat.Xml => WrapXml(content, skills),
            OutputFormat.Plain => WrapPlain(content, skills),
            _ => WrapMarkdown(content, skills)
        };
    }

    private static IReadOnlyList<string> GetSearchRoots(string rootDirectory)
    {
        var roots = new List<string>
        {
            Path.Combine(rootDirectory, ".agents", "skills"),
            Path.Combine(rootDirectory, ".agent", "skills"),
            Path.Combine(rootDirectory, ".claude", "skills")
        };
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            roots.Add(Path.Combine(profile, ".agents", "skills"));
            roots.Add(Path.Combine(profile, ".agent", "skills"));
            roots.Add(Path.Combine(profile, ".claude", "skills"));
        }

        return roots;
    }

    private static bool IsExplicitPath(string specification) =>
        Path.IsPathRooted(specification)
        || specification.EndsWith("SKILL.md", StringComparison.OrdinalIgnoreCase)
        || specification.Contains(Path.DirectorySeparatorChar)
        || specification.Contains(Path.AltDirectorySeparatorChar);

    private static async Task<LoadedSkill> ReadAsync(string path, string name, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Skill file was not found: {path}");
        var fullPath = Path.GetFullPath(path);
        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return new LoadedSkill(name, fullPath, content.TrimEnd());
    }

    private static string WrapMarkdown(string content, IReadOnlyList<LoadedSkill> skills) =>
        $"## Loaded Skills\n\n{string.Join("\n\n", skills.Select(skill => $"### {skill.Name}\n\n_Source: `{skill.SourcePath}`_\n\n{skill.Content}"))}\n\n---\n\n{content}";

    private static string WrapPlain(string content, IReadOnlyList<LoadedSkill> skills) =>
        $"LOADED SKILLS\n\n{string.Join("\n\n", skills.Select(skill => $"[{skill.Name}] {skill.SourcePath}\n{skill.Content}"))}\n\n---\n\n{content}";

    private static string WrapJson(string content, IReadOnlyList<LoadedSkill> skills)
    {
        var document = JsonNode.Parse(content)?.AsObject()
            ?? throw new JsonException("Generated JSON context must be an object.");
        document["skills"] = new JsonArray(skills.Select(skill => (JsonNode)new JsonObject
        {
            ["name"] = skill.Name,
            ["source"] = skill.SourcePath,
            ["content"] = skill.Content
        }).ToArray());
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string WrapXml(string content, IReadOnlyList<LoadedSkill> skills)
    {
        var document = XDocument.Parse(content);
        var element = new XElement("skills", skills.Select(skill => new XElement("skill",
            new XAttribute("name", skill.Name),
            new XAttribute("source", skill.SourcePath),
            new XCData(skill.Content))));
        document.Root?.AddFirst(element);
        return document.ToString(SaveOptions.None);
    }
}