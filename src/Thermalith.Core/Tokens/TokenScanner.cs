using System.Text.RegularExpressions;
using Thermalith.Core.Model;

namespace Thermalith.Core.Tokens;

/// <summary>
/// Auto-discovers the <c>{tokens}</c> a template references by scanning element content (§6.5).
/// The editor uses this to auto-populate the declared token contract (§8); the author may then
/// annotate it. Scanning order is document/z-order so the contract reads top-to-bottom.
/// </summary>
public static partial class TokenScanner
{
    [GeneratedRegex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex TokenPattern();

    /// <summary>Distinct token names referenced anywhere in the document, in first-seen order.</summary>
    public static IReadOnlyList<string> Scan(LabelDocument doc)
    {
        var seen = new List<string>();
        var set = new HashSet<string>(StringComparer.Ordinal);

        void Add(string? content)
        {
            if (string.IsNullOrEmpty(content)) return;
            foreach (Match m in TokenPattern().Matches(content))
            {
                var name = m.Groups[1].Value;
                if (set.Add(name)) seen.Add(name);
            }
        }

        foreach (var el in doc.Elements)
        {
            switch (el)
            {
                case TextElement t: Add(t.Props.Content); break;
                case BarcodeElement b: Add(b.Props.Value); break;
                case QrElement q: Add(q.Props.Value); break;
                case TableElement tab when tab.Props.Cells is { } cells:
                    foreach (var row in cells)
                        foreach (var cell in row)
                            Add(cell.Content);
                    break;
            }
        }

        return seen;
    }

    /// <summary>
    /// Build a merged token contract: every scanned token, preserving existing annotations from
    /// <see cref="LabelDocument.Tokens"/> and appending undeclared ones as bare <see cref="TokenDecl"/>s.
    /// </summary>
    public static List<TokenDecl> BuildContract(LabelDocument doc)
    {
        var declared = (doc.Tokens ?? []).ToDictionary(t => t.Name, StringComparer.Ordinal);
        var result = new List<TokenDecl>();
        foreach (var name in Scan(doc))
            result.Add(declared.TryGetValue(name, out var existing) ? existing : new TokenDecl { Name = name });
        // Keep any declared-but-unused tokens too (author may have added them deliberately).
        foreach (var decl in doc.Tokens ?? [])
            if (!result.Any(t => t.Name == decl.Name))
                result.Add(decl);
        return result;
    }
}
