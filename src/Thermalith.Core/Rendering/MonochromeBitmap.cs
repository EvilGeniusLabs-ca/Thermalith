namespace Thermalith.Core.Rendering;

/// <summary>
/// Model-agnostic 1bpp raster — the renderer's output and the clean hand-off seam into
/// <c>Niimbot.Net</c>'s encoder. Row-major, MSB-first within each byte, <c>1 = burn (black)</c>,
/// rows padded to a byte boundary. Exact bit order/padding are verified against a real device
/// capture before trusting any third-party docs. See build spec §6.3.5.
/// </summary>
public sealed record MonochromeBitmap(int WidthPx, int HeightPx, byte[] Packed);
