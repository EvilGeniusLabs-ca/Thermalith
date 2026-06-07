using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace Thermalith.Core.Rendering;

/// <summary>
/// Produces the raw module geometry for barcodes and QR codes at <b>one px per module</b>, so the
/// renderer can snap modules to whole device pixels itself (§6.3.3) rather than letting a writer
/// rescale and blur the bars. 1D symbologies and QR go through ZXing.Net; QR falls back to QRCoder
/// if ZXing rejects the payload.
/// </summary>
public static class Barcodes
{
    /// <summary>
    /// Encode a 1D symbology to its bar pattern (<c>true = black bar</c>), one entry per narrowest
    /// module, with no added quiet zone (the renderer adds the quiet zone in device px).
    /// </summary>
    public static bool[] Encode1D(string symbology, string value)
    {
        var format = symbology?.ToLowerInvariant() switch
        {
            "code128" => BarcodeFormat.CODE_128,
            "code39" => BarcodeFormat.CODE_39,
            "ean13" => BarcodeFormat.EAN_13,
            "ean8" => BarcodeFormat.EAN_8,
            "upca" => BarcodeFormat.UPC_A,
            "upce" => BarcodeFormat.UPC_E,
            "itf" => BarcodeFormat.ITF,
            "codabar" => BarcodeFormat.CODABAR,
            _ => BarcodeFormat.CODE_128,
        };

        // width=0 → natural 1px-per-module; height=1 → single row; MARGIN=0 → no side quiet zone.
        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 };
        var matrix = new MultiFormatWriter().encode(value, format, 0, 1, hints);
        var pattern = new bool[matrix.Width];
        for (var i = 0; i < matrix.Width; i++)
            pattern[i] = matrix[i, 0];
        return pattern;
    }

    /// <summary>
    /// Encode a QR payload to a square module matrix indexed <c>[x, y]</c> (<c>true = black</c>), with
    /// no quiet zone. <paramref name="encoding"/> <c>hex</c> treats the value as a hex byte string.
    /// </summary>
    public static bool[,] EncodeQr(string value, string encoding, string ecLevel)
    {
        var payload = string.Equals(encoding, "hex", StringComparison.OrdinalIgnoreCase)
            ? HexToLatin1(value)
            : value;

        try
        {
            return EncodeQrZXing(payload, ecLevel);
        }
        catch
        {
            return EncodeQrQRCoder(payload, ecLevel);
        }
    }

    private static bool[,] EncodeQrZXing(string payload, string ecLevel)
    {
        var ec = ecLevel?.ToUpperInvariant() switch
        {
            "L" => ErrorCorrectionLevel.L,
            "Q" => ErrorCorrectionLevel.Q,
            "H" => ErrorCorrectionLevel.H,
            _ => ErrorCorrectionLevel.M,
        };

        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.CHARACTER_SET] = "ISO-8859-1",
        };

        var qr = ZXing.QrCode.Internal.Encoder.encode(payload, ec, hints);
        var matrix = qr.Matrix ?? throw new InvalidOperationException("QR encode produced no matrix.");
        var n = matrix.Width;
        var modules = new bool[n, n];
        for (var y = 0; y < matrix.Height; y++)
            for (var x = 0; x < n; x++)
                modules[x, y] = matrix[x, y] == 1;
        return modules;
    }

    private static bool[,] EncodeQrQRCoder(string payload, string ecLevel)
    {
        var ecc = ecLevel?.ToUpperInvariant() switch
        {
            "L" => QRCoder.QRCodeGenerator.ECCLevel.L,
            "Q" => QRCoder.QRCodeGenerator.ECCLevel.Q,
            "H" => QRCoder.QRCodeGenerator.ECCLevel.H,
            _ => QRCoder.QRCodeGenerator.ECCLevel.M,
        };

        using var gen = new QRCoder.QRCodeGenerator();
        var data = gen.CreateQrCode(payload, ecc);
        var raw = data.ModuleMatrix;        // includes a 4-module quiet zone
        const int qz = 4;
        var n = raw.Count - 2 * qz;
        var modules = new bool[n, n];
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                modules[x, y] = raw[y + qz][x + qz];
        return modules;
    }

    private static string HexToLatin1(string hex)
    {
        var clean = hex.Replace(" ", "").Replace("-", "");
        if (clean.Length % 2 != 0) throw new System.FormatException("Hex QR payload must have an even number of digits.");
        var sb = new StringBuilder(clean.Length / 2);
        for (var i = 0; i < clean.Length; i += 2)
            sb.Append((char)Convert.ToByte(clean.Substring(i, 2), 16));
        return sb.ToString();
    }
}
