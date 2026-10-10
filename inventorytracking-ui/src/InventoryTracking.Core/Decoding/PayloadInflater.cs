using System.IO.Compression;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Decoding;

/// <summary>Turns the <c>zlib+base64</c> data field back into JSON bytes, enforcing the size limits.</summary>
internal static class PayloadInflater
{
    public static byte[] Inflate(string base64, long? rawBytes)
    {
        if (rawBytes is > DecodeLimits.MaxRawBytes)
        {
            throw new ExportDecodeException(IssueKind.TooLarge, $"rawBytes {rawBytes} is over the {DecodeLimits.MaxRawBytes} byte limit.");
        }

        if (rawBytes is < 0)
        {
            throw new ExportDecodeException(IssueKind.Invalid, "rawBytes is negative.");
        }

        var limit = rawBytes ?? DecodeLimits.MaxRawBytes;
        var inflated = ReadLimited(FromBase64(base64), limit);
        if (rawBytes is not null && inflated.Length != rawBytes)
        {
            throw new ExportDecodeException(IssueKind.Invalid, $"rawBytes mismatch: header says {rawBytes}, got {inflated.Length}.");
        }

        return inflated;
    }

    private static byte[] FromBase64(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new ExportDecodeException(IssueKind.Invalid, "The data field is not valid base64.");
        }
    }

    private static byte[] ReadLimited(byte[] compressed, long limit)
    {
        try
        {
            using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = zlib.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > limit)
                {
                    throw new ExportDecodeException(IssueKind.TooLarge, $"The data inflates to more than the allowed {limit} bytes.");
                }

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            throw new ExportDecodeException(IssueKind.Invalid, "The data field is not valid zlib data.");
        }
    }
}
