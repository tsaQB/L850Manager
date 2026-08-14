using System.Globalization;
using System.Text.RegularExpressions;
using L850Manager.Models;

namespace L850Manager.Services;

public static partial class SmsParser
{
    public static IReadOnlyList<SmsMessage> ParseTextModeList(string response)
    {
        string normalized = response.Replace("\r", string.Empty);
        MatchCollection headers = Header().Matches(normalized);
        if (headers.Count == 0 && normalized.Contains("+CMGL:", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("The modem returned SMS records in an unsupported format.");

        List<SmsMessage> messages = [];
        for (int i = 0; i < headers.Count; i++)
        {
            Match header = headers[i];
            int bodyStart = header.Index + header.Length;
            int terminator = normalized.LastIndexOf("\nOK", StringComparison.OrdinalIgnoreCase);
            int bodyEnd = i + 1 < headers.Count ? headers[i + 1].Index : terminator >= bodyStart ? terminator : normalized.Length;
            if (bodyEnd < 0) bodyEnd = normalized.Length;
            string body = normalized[bodyStart..bodyEnd].Trim('\n', ' ', '|');
            _ = int.TryParse(header.Groups[1].Value, out int index);
            messages.Add(new(index, header.Groups[2].Value, header.Groups[3].Value, ParseTimestamp(header.Groups[4].Value), body));
        }
        return messages.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Index).ToArray();
    }

    private static DateTimeOffset? ParseTimestamp(string value)
    {
        Match match = Timestamp().Match(value);
        if (!match.Success) return null;
        if (!DateTime.TryParseExact(match.Groups[1].Value, "yy/MM/dd,HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime clock))
            return null;

        clock = DateTime.SpecifyKind(clock, DateTimeKind.Unspecified);
        if (!match.Groups[2].Success)
            return new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock));

        int quarterHours = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (quarterHours > 56) return null;
        TimeSpan offset = TimeSpan.FromMinutes(quarterHours * 15 * (match.Groups[2].Value == "-" ? -1 : 1));
        return new DateTimeOffset(clock, offset);
    }

    // L850 commonly emits an unquoted empty alpha field (,,), while other firmware emits "",.
    [GeneratedRegex("\\+CMGL:\\s*(\\d+),\\s*\"([^\"]+)\",\\s*\"([^\"]*)\",\\s*(?:\"[^\"]*\")?,\\s*\"([^\"]*)\"\\s*\\n", RegexOptions.IgnoreCase)]
    private static partial Regex Header();

    [GeneratedRegex("^(\\d{2}/\\d{2}/\\d{2},\\d{2}:\\d{2}:\\d{2})(?:([+-])(\\d{2}))?$")]
    private static partial Regex Timestamp();
}
