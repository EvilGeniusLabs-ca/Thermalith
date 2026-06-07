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
public sealed partial class TokenResolver
{
    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex TokenPattern();

    private readonly IReadOnlyDictionary<string, object?>? _data;
    private readonly IReadOnlyDictionary<string, string>? _bindings;
    private readonly Dictionary<string, TokenDecl> _decls;

    public TokenResolver(
        IReadOnlyDictionary<string, object?>? data,
        IReadOnlyDictionary<string, string>? bindings = null,
        IEnumerable<TokenDecl>? decls = null)
    {
        _data = data;
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

    /// <summary>Substitute every <c>{token}</c> in <paramref name="content"/>.</summary>
    public string Substitute(string? content)
    {
        if (string.IsNullOrEmpty(content)) return content ?? "";
        return TokenPattern().Replace(content, m => ResolveToken(m.Groups[1].Value));
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
