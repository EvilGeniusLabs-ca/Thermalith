namespace Niimbot.Net.Profiles;

/// <summary>
/// Known NIIMBOT models we carry a profile for. Deliberately a small set (B1 is the Phase-1
/// target, spec §12); the broader device library can grow as captures are gathered. Unknown
/// devices resolve to <see cref="Unknown"/> and fall back to a generic profile.
/// </summary>
public enum PrinterModel
{
    Unknown,
    B1,
    B1_Pro,
    B1_SE,
    B21,
    B21S,
    D11,
    D110,
}
