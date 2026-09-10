using Microsoft.ML.Tokenizers;

namespace Codemap.Core;

internal static class TokenCounter
{
    private static readonly Tokenizer Tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

    public static int Count(string content) => Tokenizer.CountTokens(content);
}