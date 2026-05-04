using System.Linq;
using CardCore.Markdown;
using Xunit;

namespace CardCore.PureTests;

public class MarkdownParserTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var result = MarkdownParser.Parse("");
        Assert.Equal(MarkdownText.Empty, result);
        Assert.Empty(result.Tokens);
        Assert.Equal("", result.Raw);
    }

    [Fact]
    public void Parse_PlainLiteral_ProducesSingleLiteralToken()
    {
        var result = MarkdownParser.Parse("hello world");
        Assert.Single(result.Tokens);
        var lit = Assert.IsType<LiteralToken>(result.Tokens[0]);
        Assert.Equal("hello world", lit.Text);
    }

    [Fact]
    public void Parse_Icon_ProducesIconToken()
    {
        var result = MarkdownParser.Parse("[points]");
        Assert.Single(result.Tokens);
        var icon = Assert.IsType<IconToken>(result.Tokens[0]);
        Assert.Equal("points", icon.Id);
    }

    [Fact]
    public void Parse_IconWithSpacesInId_ProducesIconToken()
    {
        var result = MarkdownParser.Parse("[red sails]");
        Assert.Single(result.Tokens);
        var icon = Assert.IsType<IconToken>(result.Tokens[0]);
        Assert.Equal("red sails", icon.Id);
    }

    [Fact]
    public void Parse_LiteralFollowedByIcon_ProducesTwoTokens()
    {
        var result = MarkdownParser.Parse("+4 [points]");
        Assert.Equal(2, result.Tokens.Count);
        Assert.Equal(new LiteralToken("+4 "), result.Tokens[0]);
        Assert.Equal(new IconToken("points"), result.Tokens[1]);
    }

    [Fact]
    public void Parse_IconFollowedByLiteral_ProducesTwoTokens()
    {
        var result = MarkdownParser.Parse("[points] earned");
        Assert.Equal(2, result.Tokens.Count);
        Assert.Equal(new IconToken("points"), result.Tokens[0]);
        Assert.Equal(new LiteralToken(" earned"), result.Tokens[1]);
    }

    [Fact]
    public void Parse_BareKeyword_ProducesKeywordTokenNullParam()
    {
        var result = MarkdownParser.Parse("#draw");
        Assert.Single(result.Tokens);
        var kw = Assert.IsType<KeywordToken>(result.Tokens[0]);
        Assert.Equal("draw", kw.Id);
        Assert.Null(kw.Param);
    }

    [Fact]
    public void Parse_KeywordWithParam_ProducesKeywordToken()
    {
        var result = MarkdownParser.Parse("#if(night)");
        Assert.Single(result.Tokens);
        var kw = Assert.IsType<KeywordToken>(result.Tokens[0]);
        Assert.Equal("if", kw.Id);
        Assert.Equal("night", kw.Param);
    }

    [Fact]
    public void Parse_KeywordEndsAtSpace()
    {
        var result = MarkdownParser.Parse("#draw two");
        Assert.Equal(2, result.Tokens.Count);
        Assert.Equal(new KeywordToken("draw", null), result.Tokens[0]);
        Assert.Equal(new LiteralToken(" two"), result.Tokens[1]);
    }

    [Fact]
    public void Parse_KeywordEndsAtPunctuation()
    {
        var result = MarkdownParser.Parse("#draw.");
        Assert.Equal(2, result.Tokens.Count);
        Assert.Equal(new KeywordToken("draw", null), result.Tokens[0]);
        Assert.Equal(new LiteralToken("."), result.Tokens[1]);
    }

    [Fact]
    public void Parse_Variable_ProducesVariableToken()
    {
        var result = MarkdownParser.Parse("${name}");
        Assert.Single(result.Tokens);
        var v = Assert.IsType<VariableToken>(result.Tokens[0]);
        Assert.Equal("name", v.Name);
    }

    [Fact]
    public void Parse_VariableInsideQuotes_KeepsQuotesAsLiterals()
    {
        var result = MarkdownParser.Parse("$\"${percent}% of cards\"");
        // $", ${percent}, % of cards, "
        Assert.Equal(3, result.Tokens.Count);
        Assert.Equal(new LiteralToken("$\""), result.Tokens[0]);
        Assert.Equal(new VariableToken("percent"), result.Tokens[1]);
        Assert.Equal(new LiteralToken("% of cards\""), result.Tokens[2]);
    }

    [Fact]
    public void Parse_DollarWithoutBrace_IsLiteral()
    {
        var result = MarkdownParser.Parse("cost $5");
        Assert.Single(result.Tokens);
        var lit = Assert.IsType<LiteralToken>(result.Tokens[0]);
        Assert.Equal("cost $5", lit.Text);
    }

    [Fact]
    public void Parse_HashWithoutId_IsLiteral()
    {
        var result = MarkdownParser.Parse("count # of cards");
        Assert.Single(result.Tokens);
        var lit = Assert.IsType<LiteralToken>(result.Tokens[0]);
        Assert.Equal("count # of cards", lit.Text);
    }

    [Fact]
    public void Parse_ColonInFreeText_IsLiteral()
    {
        var result = MarkdownParser.Parse("cat:val");
        Assert.Single(result.Tokens);
        var lit = Assert.IsType<LiteralToken>(result.Tokens[0]);
        Assert.Equal("cat:val", lit.Text);
    }

    [Fact]
    public void TryParse_UnbalancedOpenBracket_ReturnsFalseWithError()
    {
        var ok = MarkdownParser.TryParse("[unfinished", out var result, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_UnbalancedCloseBracket_ReturnsFalseWithError()
    {
        var ok = MarkdownParser.TryParse("stray]", out var result, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_UnclosedVariable_ReturnsFalseWithError()
    {
        var ok = MarkdownParser.TryParse("${unfinished", out var result, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void Parse_UnbalancedBracket_Throws()
    {
        Assert.ThrowsAny<System.Exception>(() => MarkdownParser.Parse("[unfinished"));
    }

    [Fact]
    public void Parse_RawIsPreserved()
    {
        var result = MarkdownParser.Parse("+4 [points] for #if(night)");
        Assert.Equal("+4 [points] for #if(night)", result.Raw);
    }

    [Fact]
    public void Parse_ConsecutiveLiteralCharacters_Merge()
    {
        var result = MarkdownParser.Parse("a b c");
        Assert.Single(result.Tokens);
        var lit = Assert.IsType<LiteralToken>(result.Tokens[0]);
        Assert.Equal("a b c", lit.Text);
    }

    [Fact]
    public void TryParse_Valid_ReturnsTrueAndNullError()
    {
        var ok = MarkdownParser.TryParse("[points]", out var result, out var error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Single(result.Tokens);
    }
}
