using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Codemap.Core;

public static class PatchContext
{
    private const string SkillResourceName = "Codemap.Core.Skills.Patch.SKILL.md";
    private static readonly string PatchInstructions = LoadPatchSkill();

    public static string Wrap(string content, OutputFormat format) => format switch
    {
        OutputFormat.Json => WrapJson(content),
        OutputFormat.Xml => WrapXml(content),
        _ => $"{PatchInstructions}\n---\n\n{content}"
    };

    private static string WrapJson(string content)
    {
        var document = JsonNode.Parse(content)?.AsObject()
            ?? throw new JsonException("Generated JSON context must be an object.");
        document["patchInstructions"] = PatchInstructions;
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string WrapXml(string content)
    {
        var document = XDocument.Parse(content);
        document.Root?.AddFirst(new XElement("patch_instructions", PatchInstructions));
        return document.ToString(SaveOptions.None);
    }

    private static string LoadPatchSkill()
    {
        using var stream = typeof(PatchContext).Assembly.GetManifestResourceStream(SkillResourceName)
            ?? throw new InvalidOperationException($"Patch skill resource '{SkillResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().TrimEnd();
    }
}