using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace JournalRecall.Api.Domain.Sync;

/// <summary>
/// The change feed's opaque, monotonic cursor (issue 0033, ADR-0013): a UTC-ticks watermark over the
/// synced entities' <c>UpdatedAt</c> columns. Encoded (url-safe base64) so clients can't be tempted to
/// interpret it — they store the returned cursor and replay it verbatim on the next pull. Server-side
/// it decodes back to the exclusive lower bound for the next pull's <c>UpdatedAt &gt; cursor</c> filter.
/// </summary>
public static class SyncCursor
{
    public static string Encode(long ticks) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(ticks.ToString(CultureInfo.InvariantCulture)));

    /// <summary>False when <paramref name="cursor"/> isn't something this server ever issued (→ 400).</summary>
    public static bool TryDecode(string cursor, out long ticks)
    {
        ticks = 0;
        Span<byte> decoded = stackalloc byte[64];
        int written;
        try
        {
            // Try refers to buffer sizing only — a malformed payload still throws.
            if (!Base64Url.TryDecodeFromChars(cursor, decoded, out written))
                return false;
        }
        catch (FormatException)
        {
            return false;
        }

        return long.TryParse(
                   Encoding.UTF8.GetString(decoded[..written]), NumberStyles.None, CultureInfo.InvariantCulture, out ticks)
               && ticks >= 0;
    }
}
