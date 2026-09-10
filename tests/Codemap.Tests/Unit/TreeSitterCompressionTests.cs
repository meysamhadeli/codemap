using Codemap.Core;

namespace Codemap.Tests;

public sealed class TreeSitterCompressionTests
{
    [Theory]
    [InlineData("sample.cs", "class Sample { public void Run() { } }")]
    [InlineData("sample.js", "function run() { return true; }")]
    [InlineData("sample.py", "def run():\n    return True")]
    [InlineData("sample.java", "class Sample { void run() {} }")]
    [InlineData("sample.go", "func run() {}")]
    [InlineData("sample.rs", "fn run() {}")]
    public void Compress_DetectsPopularLanguage(string fileName, string source)
    {
        var compressed = CodeCompressor.Compress(source, fileName, detectLanguage: true);

        compressed.ShouldContain(source.Split('\n')[0].Split('{')[0].Trim());
    }

    [Fact]
    public void Compress_LeavesUnknownExtensionUnchanged()
    {
        const string source = "not source code";

        CodeCompressor.Compress(source, "sample.unknown", detectLanguage: true).ShouldBe(source);
    }
}