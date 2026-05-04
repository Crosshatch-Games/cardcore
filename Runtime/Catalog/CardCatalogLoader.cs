using System;
using System.Collections.Generic;
using System.IO;
using CardCore.Markdown;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CardCore.Catalog;

public static class CardCatalogLoader
{
    public static CardCatalog LoadFromDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("directoryPath must be non-empty.", nameof(directoryPath));
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var errors = new List<CardCatalogLoadError>();
        var warnings = new List<string>();
        var defs = new List<CardDefinition>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            string source = Path.GetFileName(path);
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                errors.Add(new CardCatalogLoadError(source, $"Failed to read file: {ex.Message}"));
                continue;
            }

            ParseInto(content, source, defs, errors, warnings, seenIds);
        }

        if (errors.Count > 0)
            throw new CardCatalogLoadException(errors);

        return new CardCatalog(defs, warnings);
    }

    public static CardCatalog LoadFromJson(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        var errors = new List<CardCatalogLoadError>();
        var warnings = new List<string>();
        var defs = new List<CardDefinition>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        ParseInto(json, "<inline>", defs, errors, warnings, seenIds);

        if (errors.Count > 0)
            throw new CardCatalogLoadException(errors);

        return new CardCatalog(defs, warnings);
    }

    public static CardCatalog LoadFromStream(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        using var reader = new StreamReader(stream);
        return LoadFromJson(reader.ReadToEnd());
    }

    public static CardDefinition LoadDefinition(JObject json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        var errors = new List<CardCatalogLoadError>();
        var warnings = new List<string>();
        var def = TryBuildDefinition(json, "<inline>", errors, warnings);
        if (errors.Count > 0 || def is null)
            throw new CardCatalogLoadException(errors);
        return def;
    }

    private static void ParseInto(
        string json,
        string source,
        List<CardDefinition> defs,
        List<CardCatalogLoadError> errors,
        List<string> warnings,
        HashSet<string> seenIds)
    {
        JToken? root;
        try
        {
            root = JToken.Parse(json);
        }
        catch (JsonException ex)
        {
            errors.Add(new CardCatalogLoadError(source, $"Invalid JSON: {ex.Message}"));
            return;
        }

        var entries = new List<JObject>();
        if (root is JArray arr)
        {
            int i = 0;
            foreach (var t in arr)
            {
                if (t is JObject jo) entries.Add(jo);
                else errors.Add(new CardCatalogLoadError($"{source}[{i}]", "Expected object."));
                i++;
            }
        }
        else if (root is JObject single)
        {
            entries.Add(single);
        }
        else
        {
            errors.Add(new CardCatalogLoadError(source, "Expected JSON object or array."));
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            string entrySource = entries.Count == 1 ? source : $"{source}[{i}]";
            var def = TryBuildDefinition(entries[i], entrySource, errors, warnings);
            if (def is null) continue;

            if (!seenIds.Add(def.Id))
            {
                errors.Add(new CardCatalogLoadError(
                    entrySource, $"Duplicate CardDefinition id '{def.Id}'."));
                continue;
            }

            defs.Add(def);
        }
    }

    private static CardDefinition? TryBuildDefinition(
        JObject jo,
        string source,
        List<CardCatalogLoadError> errors,
        List<string> warnings)
    {
        var idTok = jo["id"];
        if (idTok is null || idTok.Type == JTokenType.Null)
        {
            errors.Add(new CardCatalogLoadError(source, "Missing required field 'id'."));
            return null;
        }
        string id = idTok.ToString();

        MarkdownText name;
        if (!TryReadMarkdown(jo, "name", source, "name", errors, out name))
            return null;
        MarkdownText flavor;
        if (!TryReadMarkdown(jo, "flavor", source, "flavor", errors, out flavor))
            return null;

        var types = ReadStringList(jo, "types");
        var costs = ReadCurrencyList(jo, "costs", source, "costs", errors, warnings);
        var rewards = ReadCurrencyList(jo, "rewards", source, "rewards", errors, warnings);
        var thresholds = ReadCurrencyList(jo, "thresholds", source, "thresholds", errors, warnings);
        var actions = ReadActionList(jo, source, errors);
        var targets = ReadMarkdownList(jo, "targets", source, errors);
        string? back = jo["back"]?.Type == JTokenType.Null ? null : jo["back"]?.ToString();
        string? rarity = jo["rarity"]?.Type == JTokenType.Null ? null : jo["rarity"]?.ToString();

        if (errors.Count > 0 && errors[errors.Count - 1].Source.StartsWith(source, StringComparison.Ordinal))
        {
            // Continue building so we capture id-validation too, then return null at end if anything failed.
        }

        try
        {
            return new CardDefinition(
                id,
                name,
                types,
                costs,
                rewards,
                thresholds,
                actions,
                targets,
                back,
                rarity,
                flavor);
        }
        catch (ArgumentException ex)
        {
            errors.Add(new CardCatalogLoadError(source, ex.Message));
            return null;
        }
    }

    private static bool TryReadMarkdown(
        JObject jo,
        string field,
        string source,
        string fieldLabel,
        List<CardCatalogLoadError> errors,
        out MarkdownText result)
    {
        result = MarkdownText.Empty;
        var tok = jo[field];
        if (tok is null || tok.Type == JTokenType.Null) return true;

        string raw;
        if (tok is JObject mdo)
        {
            raw = mdo["raw"]?.ToString() ?? string.Empty;
        }
        else if (tok.Type == JTokenType.String)
        {
            raw = tok.ToString();
        }
        else
        {
            errors.Add(new CardCatalogLoadError(source,
                $"Field '{fieldLabel}' must be string or {{ raw: string }}."));
            return false;
        }

        if (!MarkdownParser.TryParse(raw, out var parsed, out var error))
        {
            errors.Add(new CardCatalogLoadError(source,
                $"Field '{fieldLabel}' invalid markdown: {error}"));
            return false;
        }
        result = parsed;
        return true;
    }

    private static IReadOnlyList<MarkdownText> ReadMarkdownList(
        JObject jo,
        string field,
        string source,
        List<CardCatalogLoadError> errors)
    {
        var tok = jo[field];
        if (tok is null || tok.Type == JTokenType.Null) return Array.Empty<MarkdownText>();
        if (tok is not JArray arr)
        {
            errors.Add(new CardCatalogLoadError(source, $"Field '{field}' must be an array."));
            return Array.Empty<MarkdownText>();
        }
        var list = new List<MarkdownText>();
        for (int i = 0; i < arr.Count; i++)
        {
            var item = arr[i];
            string raw;
            if (item is JObject mdo) raw = mdo["raw"]?.ToString() ?? string.Empty;
            else if (item.Type == JTokenType.String) raw = item.ToString();
            else
            {
                errors.Add(new CardCatalogLoadError(source,
                    $"Field '{field}[{i}]' must be string or {{ raw: string }}."));
                continue;
            }
            if (!MarkdownParser.TryParse(raw, out var parsed, out var error))
            {
                errors.Add(new CardCatalogLoadError(source,
                    $"Field '{field}[{i}]' invalid markdown: {error}"));
                continue;
            }
            list.Add(parsed);
        }
        return list;
    }

    private static IReadOnlyList<string> ReadStringList(JObject jo, string field)
    {
        var tok = jo[field];
        if (tok is null || tok.Type == JTokenType.Null) return Array.Empty<string>();
        if (tok is not JArray arr) return Array.Empty<string>();
        var list = new List<string>(arr.Count);
        foreach (var item in arr)
            if (item.Type == JTokenType.String) list.Add(item.ToString());
        return list;
    }

    private static IReadOnlyList<CurrencyAmount> ReadCurrencyList(
        JObject jo,
        string field,
        string source,
        string fieldLabel,
        List<CardCatalogLoadError> errors,
        List<string> warnings)
    {
        var tok = jo[field];
        if (tok is null || tok.Type == JTokenType.Null) return Array.Empty<CurrencyAmount>();
        if (tok is not JArray arr)
        {
            errors.Add(new CardCatalogLoadError(source, $"Field '{fieldLabel}' must be an array."));
            return Array.Empty<CurrencyAmount>();
        }

        var list = new List<CurrencyAmount>();
        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] is not JObject co)
            {
                errors.Add(new CardCatalogLoadError(source,
                    $"Field '{fieldLabel}[{i}]' must be an object."));
                continue;
            }
            var amountTok = co["amount"];
            var typeTok = co["type"];
            bool hasAmount = amountTok is not null && amountTok.Type != JTokenType.Null;
            bool hasType = typeTok is not null && typeTok.Type != JTokenType.Null
                && !string.IsNullOrWhiteSpace(typeTok.ToString());

            if (hasAmount && !hasType)
            {
                warnings.Add($"{source}: '{fieldLabel}[{i}]' has amount but no type — entry skipped.");
                continue;
            }
            if (!hasAmount && hasType)
            {
                warnings.Add($"{source}: '{fieldLabel}[{i}]' has type but no amount — entry skipped.");
                continue;
            }
            if (!hasAmount && !hasType)
            {
                continue;
            }

            int amount;
            try { amount = (int)amountTok!; }
            catch
            {
                errors.Add(new CardCatalogLoadError(source,
                    $"Field '{fieldLabel}[{i}].amount' must be an integer."));
                continue;
            }

            try
            {
                list.Add(new CurrencyAmount(amount, typeTok!.ToString()));
            }
            catch (ArgumentException ex)
            {
                errors.Add(new CardCatalogLoadError(source,
                    $"Field '{fieldLabel}[{i}]' invalid: {ex.Message}"));
            }
        }
        return list;
    }

    private static IReadOnlyList<Action> ReadActionList(
        JObject jo,
        string source,
        List<CardCatalogLoadError> errors)
    {
        var tok = jo["actions"];
        if (tok is null || tok.Type == JTokenType.Null) return Array.Empty<Action>();
        if (tok is not JArray arr)
        {
            errors.Add(new CardCatalogLoadError(source, "Field 'actions' must be an array."));
            return Array.Empty<Action>();
        }
        var list = new List<Action>();
        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] is not JObject ao)
            {
                errors.Add(new CardCatalogLoadError(source, $"Field 'actions[{i}]' must be an object."));
                continue;
            }
            string verb = ao["verb"]?.ToString() ?? string.Empty;
            var payloadTok = ao["payload"];
            JObject payload;
            if (payloadTok is null || payloadTok.Type == JTokenType.Null)
                payload = new JObject();
            else if (payloadTok is JObject p)
                payload = p;
            else
            {
                errors.Add(new CardCatalogLoadError(source,
                    $"Field 'actions[{i}].payload' must be a JSON object."));
                continue;
            }
            try
            {
                list.Add(new Action(verb, payload));
            }
            catch (ArgumentException ex)
            {
                errors.Add(new CardCatalogLoadError(source,
                    $"Field 'actions[{i}]' invalid: {ex.Message}"));
            }
        }
        return list;
    }
}
