using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Catalog;

public sealed class CardCatalogLoadException : Exception
{
    public IReadOnlyList<CardCatalogLoadError> Errors { get; }

    public CardCatalogLoadException(IReadOnlyList<CardCatalogLoadError> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors ?? Array.Empty<CardCatalogLoadError>();
    }

    private static string BuildMessage(IReadOnlyList<CardCatalogLoadError> errors)
    {
        if (errors is null || errors.Count == 0)
            return "Card catalog load failed.";
        return "Card catalog load failed:\n" +
               string.Join("\n", errors.Select(e => $"  - {e.Source}: {e.Message}"));
    }
}

public readonly record struct CardCatalogLoadError(string Source, string Message);
