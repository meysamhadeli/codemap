using TreeSitter;

namespace Codemap.Core;

public static class CodeCompressor
{
    private static readonly IReadOnlyDictionary<string, string> LanguageByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "c-sharp", [".js"] = "javascript", [".jsx"] = "javascript", [".ts"] = "typescript", [".tsx"] = "tsx",
        [".py"] = "python", [".java"] = "java", [".go"] = "go", [".rs"] = "rust", [".cpp"] = "cpp", [".cc"] = "cpp", [".c"] = "c",
        [".h"] = "c", [".hpp"] = "cpp", [".rb"] = "ruby", [".php"] = "php", [".kt"] = "kotlin", [".kts"] = "kotlin",
        [".html"] = "html", [".css"] = "css", [".json"] = "json", [".sh"] = "bash", [".bash"] = "bash", [".scala"] = "scala", [".swift"] = "swift", [".toml"] = "toml"
    };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> DeclarationTypes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["c-sharp"] = ["class_declaration", "interface_declaration", "struct_declaration", "enum_declaration", "method_declaration", "property_declaration"],
        ["javascript"] = ["class_declaration", "function_declaration", "method_definition", "lexical_declaration"],
        ["typescript"] = ["class_declaration", "function_declaration", "method_definition", "interface_declaration", "type_alias_declaration", "lexical_declaration"],
        ["tsx"] = ["class_declaration", "function_declaration", "method_definition", "interface_declaration", "lexical_declaration"],
        ["python"] = ["class_definition", "function_definition", "decorated_definition"],
        ["java"] = ["class_declaration", "interface_declaration", "enum_declaration", "method_declaration", "field_declaration"],
        ["go"] = ["type_declaration", "function_declaration", "method_declaration", "var_declaration", "const_declaration"],
        ["rust"] = ["struct_item", "enum_item", "trait_item", "impl_item", "function_item", "const_item", "static_item"],
        ["cpp"] = ["class_specifier", "struct_specifier", "function_definition", "declaration"],
        ["c"] = ["struct_specifier", "function_definition", "declaration"],
        ["ruby"] = ["class", "module", "method", "singleton_method", "assignment"],
        ["php"] = ["class_declaration", "interface_declaration", "function_definition", "method_declaration"],
        ["kotlin"] = ["class_declaration", "function_declaration", "property_declaration"],
        ["json"] = ["pair", "object", "array"],
        ["html"] = ["element", "script_element", "style_element"],
        ["css"] = ["rule_set", "media_statement", "declaration"],
        ["bash"] = ["function_definition", "variable_assignment", "command"],
        ["yaml"] = ["block_mapping_pair", "block_sequence_item"]
        , ["scala"] = ["class_definition", "trait_definition", "object_definition", "function_definition", "val_definition", "var_definition"]
        , ["swift"] = ["class_declaration", "struct_declaration", "enum_declaration", "protocol_declaration", "function_declaration", "property_declaration"]
        , ["toml"] = ["pair", "table", "array_table"]
    };

    public static string Compress(string content)
    {
        return Compress(content, "c-sharp");
    }

    public static string Compress(string content, string languageId)
    {
        if (!DeclarationTypes.ContainsKey(languageId)) return content;

        try
        {
            using var language = CreateLanguage(languageId);
            using var parser = new Parser(language);
            using var tree = parser.Parse(content);
            if (tree is null) return content;

            var declarations = FindDeclarations(tree.RootNode, DeclarationTypes[languageId])
                .Select(node => node.Text.Trim())
                .Distinct()
                .ToArray();
            return declarations.Length == 0 ? content : string.Join(Environment.NewLine + Environment.NewLine, declarations);
        }
        catch (EntryPointNotFoundException)
        {
            return content;
        }
    }

    public static string Compress(string content, string fileName, bool detectLanguage)
    {
        var extension = Path.GetExtension(fileName);
        return detectLanguage && LanguageByExtension.TryGetValue(extension, out var language)
            ? Compress(content, language)
            : content;
    }

    private static IEnumerable<Node> FindDeclarations(Node node, HashSet<string> declarationTypes)
    {
        if (declarationTypes.Contains(node.Type)) yield return node;
        foreach (var child in node.NamedChildren)
        {
            foreach (var declaration in FindDeclarations(child, declarationTypes)) yield return declaration;
        }
    }

    private static Language CreateLanguage(string languageId) => languageId switch
    {
        "c-sharp" => new Language("tree-sitter-c-sharp", "tree_sitter_c_sharp"),
        "javascript" => new Language("tree-sitter-javascript", "tree_sitter_javascript"),
        "typescript" => new Language("tree-sitter-typescript", "tree_sitter_typescript"),
        "tsx" => new Language("tree-sitter-tsx", "tree_sitter_tsx"),
        "python" => new Language("tree-sitter-python", "tree_sitter_python"),
        "java" => new Language("tree-sitter-java", "tree_sitter_java"),
        "go" => new Language("tree-sitter-go", "tree_sitter_c"),
        "rust" => new Language("tree-sitter-rust", "tree_sitter_rust"),
        "cpp" => new Language("tree-sitter-cpp", "tree_sitter_cpp"),
        "c" => new Language("tree-sitter-c", "tree_sitter_c"),
        "ruby" => new Language("tree-sitter-ruby", "tree_sitter_ruby"),
        "php" => new Language("tree-sitter-php", "tree_sitter_php"),
        "html" => new Language("tree-sitter-html", "tree_sitter_html"),
        "css" => new Language("tree-sitter-css", "tree_sitter_css"),
        "json" => new Language("tree-sitter-json", "tree_sitter_json"),
        "bash" => new Language("tree-sitter-bash", "tree_sitter_bash"),
        "scala" => new Language("tree-sitter-scala", "tree_sitter_scala"),
        "swift" => new Language("tree-sitter-swift", "tree_sitter_swift"),
        _ => new Language(languageId)
    };
}