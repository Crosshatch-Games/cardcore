namespace CardCore.Markdown;

public abstract record MarkdownToken;

public sealed record LiteralToken(string Text) : MarkdownToken;

public sealed record IconToken(string Id) : MarkdownToken;

public sealed record KeywordToken(string Id, string? Param) : MarkdownToken;

public sealed record VariableToken(string Name) : MarkdownToken;

public sealed record TypeRefToken(string Category, string Value) : MarkdownToken;
