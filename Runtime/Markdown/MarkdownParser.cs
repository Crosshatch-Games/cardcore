using System;
using System.Collections.Generic;
using System.Text;

namespace CardCore.Markdown;

public static class MarkdownParser
{
    public static MarkdownText Parse(string raw)
    {
        if (!TryParse(raw, out var result, out var error))
            throw new FormatException(error ?? "Invalid Cardcore Markdown.");
        return result;
    }

    public static bool TryParse(string raw, out MarkdownText result, out string? error)
    {
        if (string.IsNullOrEmpty(raw))
        {
            result = MarkdownText.Empty;
            error = null;
            return true;
        }

        var tokens = new List<MarkdownToken>();
        var literal = new StringBuilder();
        int i = 0;

        void FlushLiteral()
        {
            if (literal.Length > 0)
            {
                tokens.Add(new LiteralToken(literal.ToString()));
                literal.Clear();
            }
        }

        while (i < raw.Length)
        {
            char c = raw[i];

            if (c == '[')
            {
                int close = raw.IndexOf(']', i + 1);
                if (close < 0)
                {
                    result = MarkdownText.Empty;
                    error = $"Unbalanced '[' at position {i}.";
                    return false;
                }
                FlushLiteral();
                tokens.Add(new IconToken(raw.Substring(i + 1, close - i - 1)));
                i = close + 1;
                continue;
            }

            if (c == ']')
            {
                result = MarkdownText.Empty;
                error = $"Unbalanced ']' at position {i}.";
                return false;
            }

            if (c == '#' && i + 1 < raw.Length && IsIdStart(raw[i + 1]))
            {
                int idStart = i + 1;
                int idEnd = idStart;
                while (idEnd < raw.Length && IsIdChar(raw[idEnd])) idEnd++;
                string id = raw.Substring(idStart, idEnd - idStart);
                string? param = null;
                int next = idEnd;
                if (next < raw.Length && raw[next] == '(')
                {
                    int paramClose = raw.IndexOf(')', next + 1);
                    if (paramClose < 0)
                    {
                        result = MarkdownText.Empty;
                        error = $"Unbalanced '(' for keyword at position {next}.";
                        return false;
                    }
                    param = raw.Substring(next + 1, paramClose - next - 1);
                    next = paramClose + 1;
                }
                FlushLiteral();
                tokens.Add(new KeywordToken(id, param));
                i = next;
                continue;
            }

            if (c == '$' && i + 1 < raw.Length && raw[i + 1] == '{')
            {
                int close = raw.IndexOf('}', i + 2);
                if (close < 0)
                {
                    result = MarkdownText.Empty;
                    error = $"Unclosed variable starting at position {i}.";
                    return false;
                }
                FlushLiteral();
                tokens.Add(new VariableToken(raw.Substring(i + 2, close - i - 2)));
                i = close + 1;
                continue;
            }

            literal.Append(c);
            i++;
        }

        FlushLiteral();

        result = new MarkdownText(raw, tokens);
        error = null;
        return true;
    }

    private static bool IsIdStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
