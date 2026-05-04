using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CardCore.Markdown;

public sealed record MarkdownText
{
    public static readonly MarkdownText Empty = new("", Array.Empty<MarkdownToken>());

    public string Raw { get; }

    [JsonIgnore]
    public IReadOnlyList<MarkdownToken> Tokens { get; }

    public MarkdownText(string Raw, IReadOnlyList<MarkdownToken> Tokens)
    {
        this.Raw = Raw ?? string.Empty;
        this.Tokens = Tokens ?? Array.Empty<MarkdownToken>();
    }

    // Used by Newtonsoft when deserializing — reconstruct tokens from Raw.
    [JsonConstructor]
    private MarkdownText(string Raw)
    {
        this.Raw = Raw ?? string.Empty;
        this.Tokens = string.IsNullOrEmpty(this.Raw)
            ? Array.Empty<MarkdownToken>()
            : MarkdownParser.Parse(this.Raw).Tokens;
    }
}
