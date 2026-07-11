using System.Globalization;
using System.Text.RegularExpressions;
using Thermalith.Core.Model;

namespace Thermalith.Core.Tokens;

/// <summary>The outcome of resolving one token reference.</summary>
public enum TokenResolution
{
    /// <summary>Filled from a supplied data value.</summary>
    FromData,

    /// <summary>Filled from the token's declared <c>default</c>.</summary>
    FromDefault,

    /// <summary>Filled from the token's <c>sample</c> — a preview placeholder, not real data.</summary>
    FromSample,

    /// <summary>No value anywhere — rendered as the literal <c>{name}</c> placeholder.</summary>
    Unresolved,
}

/// <summary>
/// Resolves <c>{tokens}</c> against a supplied data row, the declared contract, and an optional
/// token→column binding remap (§6.5). Precedence (first present wins): data → token default →
/// token sample (preview placeholder) → visible <c>{name}</c> placeholder. Tracks which tokens
/// failed to resolve so the validator can flag required-but-unresolved tokens (§6.7).
/// </summary>
/// <remarks>
/// Three token forms are recognised (GitHub #7 data-merge):
/// <list type="bullet">
///   <item><c>{name}</c> — bare identifier; binds to a column / declared token by name.</item>
///   <item><c>{"any name"}</c> — quoted; binds by column name and may contain spaces/punctuation
///     (needed for CSV headers like <c>Column A</c>).</item>
///   <item><c>{3}</c> — 1-based ordinal; binds to the Nth column positionally (the only way to reach
///     a blank-header or duplicate-named column).</item>
/// </list>
/// Braces are escaped by doubling: <c>{{</c> renders a literal <c>{</c> and <c>}}</c> a literal <c>}</c>,
/// so <c>{{name}}</c> prints the literal text <c>{name}</c> rather than resolving it.
/// </remarks>
public sealed partial class TokenResolver
{
    // Order matters: escapes first, then quoted / ordinal / bare-identifier token forms.
    [GeneratedRegex("""\{\{|\}\}|\{\s*"(?<q>[^"]*)"\s*\}|\{\s*(?<ord>\d+)\s*\}|\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}""")]
    private static partial Regex TokenPattern();

    private readonly IReadOnlyDictionary<string, object?>? _data;
    private readonly IReadOnlyList<object?>? _ordinals;
    private readonly IReadOnlyDictionary<string, string>? _bindings;
    private readonly Dictionary<string, TokenDecl> _decls;

    public TokenResolver(
        IReadOnlyDictionary<string, object?>? data,
        IReadOnlyDictionary<string, string>? bindings = null,
        IEnumerable<TokenDecl>? decls = null,
        IReadOnlyList<object?>? ordinals = null)
    {
        _data = data;
        _ordinals = ordinals;
        _bindings = bindings;
        _decls = (decls ?? []).ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    /// <summary>Tokens that did not resolve to real data (sample or missing). Keyed by token name.</summary>
    public IReadOnlyDictionary<string, TokenResolution> Unresolved => _unresolved;
    private readonly Dictionary<string, TokenResolution> _unresolved = new(StringComparer.Ordinal);

    /// <summary>Resolve a single token name to its string value, recording its resolution status.</summary>
    public string ResolveToken(string name)
    {
        var (value, status) = Lookup(name);
        if (status is TokenResolution.FromSample or TokenResolution.Unresolved)
            _unresolved[name] = status;
        return value;
    }

    /// <summary>Substitute every <c>{token}</c> in <paramref name="content"/>, honouring brace escapes.</summary>
    public string Substitute(string? content)
    {
        if (string.IsNullOrEmpty(content)) return content ?? "";
        return TokenPattern().Replace(content, Evaluate);
    }

    private string Evaluate(Match m)
    {
        switch (m.Value)
        {
            case "{{": return "{";
            case "}}": return "}";
        }

        if (m.Groups["q"] is { Success: true } q) return ResolveNamed(q.Value, m.Value);
        if (m.Groups["ord"] is { Success: true } ord && int.TryParse(ord.Value, out var n))
            return ResolveOrdinal(n, m.Value);
        return ResolveNamed(m.Groups["name"].Value, m.Value);
    }

    /// <summary>Resolve a by-name token, preserving the author's exact literal (quoted or bare) on a miss.</summary>
    private string ResolveNamed(string name, string literal)
    {
        var (value, status) = Lookup(name);
        if (status is TokenResolution.FromSample or TokenResolution.Unresolved)
            _unresolved[name] = status;
        return status is TokenResolution.Unresolved ? literal : value;
    }

    /// <summary>Resolve a 1-based ordinal token to the Nth data column, recording misses as unresolved.</summary>
    private string ResolveOrdinal(int ordinal, string literal)
    {
        if (_ordinals is not null && ordinal >= 1 && ordinal <= _ordinals.Count)
            return Format(_ordinals[ordinal - 1]); // present (incl. an empty cell) → real data, even if ""
        _unresolved["#" + ordinal] = TokenResolution.Unresolved;
        return literal; // no such column → leave the literal {n} visible
    }

    private (string Value, TokenResolution Status) Lookup(string name)
    {
        var column = _bindings is not null && _bindings.TryGetValue(name, out var mapped) ? mapped : name;

        if (_data is not null && TryGetData(column, out var raw))
            return (Format(raw), TokenResolution.FromData);

        if (_decls.TryGetValue(name, out var decl))
        {
            if (decl.Default is not null) return (decl.Default, TokenResolution.FromDefault);
            if (decl.Sample is not null) return (decl.Sample, TokenResolution.FromSample);
        }

        return ("{" + name + "}", TokenResolution.Unresolved);
    }

    private bool TryGetData(string key, out object? value)
    {
        if (_data!.TryGetValue(key, out value)) return true;
        // Case-insensitive fallback so column casing differences don't break binding.
        foreach (var kv in _data)
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        value = null;
        return false;
    }

    private static string Format(object? raw) => raw switch
    {
        null => "",
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => raw.ToString() ?? "",
    };
}
