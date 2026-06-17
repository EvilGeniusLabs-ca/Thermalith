namespace Niimbot.Net.Profiles;

/// <summary>
/// The small set of behavioural facts NIIMBOT's device list does NOT carry, and that we therefore
/// hand-maintain (worklist §A). Everything geometric is derived from the catalogue; these two maps
/// are the only model knowledge left in code:
/// <list type="bullet">
///   <item><b>Verified</b> — confirmed driving real hardware (stamped onto the catalogue by the importer).</item>
///   <item><b>D110 print task</b> — the small side-fed D-series want the D110 print sequence rather than
///   the B1 one; this is not cleanly per-series in the source data (B21 = B1 but B21S = D110), so it is
///   an explicit id list with a B1 default.</item>
/// </list>
/// Add new ids here as models are verified / characterised — geometry never needs touching.
/// </summary>
public static class KnownPrinterFacts
{
    /// <summary>Model ids confirmed on real hardware. B1 (4096) and B4 (6656) as of 2026-06-14; D11_H
    /// (528) as of 2026-06-17 — the first verified D110-family / side-fed / 300-dpi unit.</summary>
    public static readonly IReadOnlySet<int> VerifiedModelIds = new HashSet<int> { 4096, 6656, 528 };

    /// <summary>
    /// Model ids that use the <see cref="PrintTaskVersion.D110"/> print sequence (2-byte PrintStart /
    /// 4-byte SetPageSize). The D11 / D110 / D101 small-label families plus the B21S. Everything not
    /// listed defaults to <see cref="PrintTaskVersion.B1"/>. Only the D11 path is hardware-checked;
    /// the rest are best-known and should be confirmed as units are tested.
    /// </summary>
    public static readonly IReadOnlySet<int> D110PrintTaskModelIds = new HashSet<int>
    {
        // B21S (B21-series body, D110 print task)
        777,
        // D11 series (528 = D11_H uses the D110M-v4 task — see D110MV4PrintTaskModelIds)
        512, 513, 514, 531,
        // D110 series
        2304, 2305, 2320,
        // D101 series
        2560, 2561,
    };

    /// <summary>
    /// Model ids that use the <see cref="PrintTaskVersion.D110MV4"/> print sequence (9-byte PrintStart,
    /// 13-byte SetPageSize with the copy count embedded, no PrintClear/PageStart/SetPrintQuantity). The
    /// D11_H is hardware-confirmed (id 528); D110_M v4, B1_PRO, B21_PRO share it per niimbluelib but are
    /// unverified here. Checked before <see cref="D110PrintTaskModelIds"/>.
    /// </summary>
    public static readonly IReadOnlySet<int> D110MV4PrintTaskModelIds = new HashSet<int>
    {
        528, // D11_H — hardware-confirmed
    };

    public static bool IsVerified(IEnumerable<int> modelIds) => modelIds.Any(VerifiedModelIds.Contains);

    public static bool UsesD110PrintTask(IEnumerable<int> modelIds) => modelIds.Any(D110PrintTaskModelIds.Contains);

    public static bool UsesD110MV4PrintTask(IEnumerable<int> modelIds) => modelIds.Any(D110MV4PrintTaskModelIds.Contains);
}
