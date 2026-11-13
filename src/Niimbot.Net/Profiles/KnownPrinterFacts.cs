namespace Niimbot.Net.Profiles;

/// <summary>
/// The small set of behavioural facts NIIMBOT's device list does NOT carry, and that we therefore
/// hand-maintain (worklist §A). Everything geometric is derived from the catalogue; what's left in
/// code is which models are hardware-verified and which print task each model uses.
///
/// <para><b>Print task.</b> Taken from niimbluelib's model-to-task table (<c>modelPrintTasks</c> and
/// <c>findPrintTask</c> in src/print_tasks/index.ts, imported 2026-11-09, GitHub #21), keyed here by
/// model id. Resolution order, as in niimbluelib with one addition:</para>
/// <list type="number">
///   <item>Our hardware-verified printers are pinned to the task they were verified on.</item>
///   <item>A model + protocol-version entry (niimbluelib: D110_M on protocol 4).</item>
///   <item>A model entry.</item>
///   <item>Not listed: protocol 4 or higher gets D110M-v4 (niimbluelib's fallback); otherwise our
///   previous best guess (D110 for a few small D-series bodies, else B1).</item>
/// </list>
/// <para>niimbluelib tasks we don't implement map onto the nearest one we do: D11_V1 (old D11 / D11S)
/// runs on our D110 path, and B21_V1 (B21), B21_L2B and H1S stay on B1 as before.</para>
/// </summary>
public static class KnownPrinterFacts
{
    /// <summary>Model ids confirmed on real hardware. B1 (4096) and B4 (6656) as of 2026-06-14; D11_H
    /// (528) as of 2026-06-17 — the first verified D110-family / side-fed / 300-dpi unit; B3S_P (272)
    /// as of 2026-07-30 — USB-C revision, fw 7.81, 203 dpi / 576-px head, B1 print task correct.</summary>
    public static readonly IReadOnlySet<int> VerifiedModelIds = new HashSet<int> { 4096, 6656, 528, 272 };

    /// <summary>Hardware-verified printers, pinned to the task they were verified on so neither a
    /// table change nor the protocol fallback can move them. B4 and B3S_P aren't in niimbluelib's table
    /// and their protocol versions aren't recorded, so without the pin the fallback could.</summary>
    private static readonly Dictionary<int, PrintTaskVersion> VerifiedTasks = new()
    {
        [4096] = PrintTaskVersion.B1,      // B1
        [6656] = PrintTaskVersion.B1,      // B4
        [272] = PrintTaskVersion.B1,       // B3S_P
        [528] = PrintTaskVersion.D110MV4,  // D11_H
    };

    /// <summary>niimbluelib model + protocol-version entries (checked before plain model entries).</summary>
    private static readonly Dictionary<(int Id, int Protocol), PrintTaskVersion> ModelProtocolTasks = new()
    {
        [(2320, 4)] = PrintTaskVersion.D110MV4, // D110_M v4
        [(512, 1)] = PrintTaskVersion.D110,     // D11 v1
        [(512, 2)] = PrintTaskVersion.D110,     // D11 v2
    };

    /// <summary>niimbluelib model entries, by model id.</summary>
    private static readonly Dictionary<int, PrintTaskVersion> ModelTasks = new()
    {
        // D11_V1 (old D11 task) - runs on our D110 path
        [512] = PrintTaskVersion.D110,   // D11
        [514] = PrintTaskVersion.D110,   // D11S
        // D110
        [777] = PrintTaskVersion.D110,   // B21S
        [776] = PrintTaskVersion.D110,   // B21S_C2B
        [2304] = PrintTaskVersion.D110,  // D110
        [2305] = PrintTaskVersion.D110,  // D110
        // B1
        [2320] = PrintTaskVersion.B1,    // D110_M (protocol 4 is above)
        [4096] = PrintTaskVersion.B1,    // B1
        [771] = PrintTaskVersion.B1,     // B21_C2B
        [775] = PrintTaskVersion.B1,     // B21_C2B
        [4608] = PrintTaskVersion.B1,    // M2_H
        [3586] = PrintTaskVersion.B1,    // N1
        [2560] = PrintTaskVersion.B1,    // D101
        // D110M_V4
        [528] = PrintTaskVersion.D110MV4,  // D11_H
        [785] = PrintTaskVersion.D110MV4,  // B21_PRO
        [4097] = PrintTaskVersion.D110MV4, // B1_PRO - also confirmed by a user, GitHub #21
        [5120] = PrintTaskVersion.D110MV4, // C1
        [5121] = PrintTaskVersion.D110MV4, // EP1C
        // Tasks we don't implement - kept on B1: B21_V1 (B21), B21_L2B, H1S
        [768] = PrintTaskVersion.B1,     // B21
        [769] = PrintTaskVersion.B1,     // B21_L2B
        [4352] = PrintTaskVersion.B1,    // H1S
    };

    /// <summary>Not in niimbluelib's table, below protocol 4: our earlier best guess of the D110 task
    /// for these small D-series bodies (Fust, D11_Pro, Betty). Unverified.</summary>
    private static readonly HashSet<int> LegacyD110Guess = [513, 531, 2561];

    public static bool IsVerified(IEnumerable<int> modelIds) => modelIds.Any(VerifiedModelIds.Contains);

    /// <summary>
    /// The print task for a model, given its ids and the protocol version it reported on connect
    /// (null or 0 = unknown). See the class remarks for the resolution order.
    /// </summary>
    public static PrintTaskVersion ResolvePrintTask(IReadOnlyCollection<int> modelIds, int? protocolVersion)
    {
        var protocol = protocolVersion is > 0 ? protocolVersion : null;

        foreach (var id in modelIds)
            if (VerifiedTasks.TryGetValue(id, out var pinned))
                return pinned;

        if (protocol is { } p)
            foreach (var id in modelIds)
                if (ModelProtocolTasks.TryGetValue((id, p), out var exact))
                    return exact;

        foreach (var id in modelIds)
            if (ModelTasks.TryGetValue(id, out var task))
                return task;

        if (protocol >= 4)
            return PrintTaskVersion.D110MV4;

        return modelIds.Any(LegacyD110Guess.Contains) ? PrintTaskVersion.D110 : PrintTaskVersion.B1;
    }
}
