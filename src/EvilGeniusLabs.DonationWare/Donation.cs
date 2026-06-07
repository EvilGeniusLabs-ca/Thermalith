namespace EvilGeniusLabs.DonationWare;

/// <summary>Supported donation providers. All resolve to a URL the launcher opens — no money is handled in-process.</summary>
public enum DonationProviderKind
{
    KoFi,
    PayPal,
    Stripe,   // hosted Payment Link only — never API keys
    Custom,
}

/// <summary>A single donation option the host app renders and the launcher can open.</summary>
public sealed record DonationProvider(
    DonationProviderKind Kind,
    string Url,
    string Label,
    string? Message = null);

/// <summary>
/// Opens a donation URL cross-platform. The default implementation uses the OS shell
/// (Windows ShellExecute, macOS <c>open</c>, Linux <c>xdg-open</c>); an Avalonia host may
/// inject one backed by <c>TopLevel.Launcher</c>. See build spec §4.2.
/// </summary>
public interface IDonationLauncher
{
    ValueTask LaunchAsync(DonationProvider provider, CancellationToken ct = default);
}
