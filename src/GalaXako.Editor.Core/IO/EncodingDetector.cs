using System.Globalization;
using System.Text;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.IO;

public static class EncodingDetector
{
    private const int SampleSize = 64 * 1024;

    static EncodingDetector() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static async Task<TextEncodingInfo> DetectAsync(string path, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[SampleSize];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var read = await stream.ReadAsync(buffer, cancellationToken);
        var sample = buffer.AsSpan(0, read);

        if (sample.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            return new TextEncodingInfo(new UTF8Encoding(true, true), true, 3, "UTF-8 BOM");
        if (sample.StartsWith(new byte[] { 0xFF, 0xFE }))
            return new TextEncodingInfo(new UnicodeEncoding(false, true, true), true, 2, "UTF-16 LE");
        if (sample.StartsWith(new byte[] { 0xFE, 0xFF }))
            return new TextEncodingInfo(new UnicodeEncoding(true, true, true), true, 2, "UTF-16 BE");

        if (LooksLikeUtf16(sample, bigEndian: false))
            return new TextEncodingInfo(new UnicodeEncoding(false, false, true), false, 0, "UTF-16 LE");
        if (LooksLikeUtf16(sample, bigEndian: true))
            return new TextEncodingInfo(new UnicodeEncoding(true, false, true), false, 0, "UTF-16 BE");

        try
        {
            _ = new UTF8Encoding(false, true).GetString(sample);
            return new TextEncodingInfo(new UTF8Encoding(false, true), false, 0, "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            var codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            var encoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            return new TextEncodingInfo(encoding, false, 0, encoding.EncodingName);
        }
    }

    private static bool LooksLikeUtf16(ReadOnlySpan<byte> sample, bool bigEndian)
    {
        if (sample.Length < 4)
            return false;

        var pairs = Math.Min(sample.Length / 2, 2048);
        var zeroes = 0;
        for (var index = 0; index < pairs; index++)
        {
            var zeroPosition = index * 2 + (bigEndian ? 0 : 1);
            if (sample[zeroPosition] == 0)
                zeroes++;
        }

        return zeroes > pairs * 0.7;
    }
}
