using System.Globalization;
using System.Text.RegularExpressions;
using L850Manager.Models;

namespace L850Manager.Services;

public sealed partial class SmsService(AtCommandService at)
{
    public async Task<IReadOnlyList<SmsMessage>> ListAsync(CancellationToken cancellationToken = default)
    {
        string response = await at.ExecuteTextModeAsync("AT+CMGL=\"ALL\"", TimeSpan.FromSeconds(10), cancellationToken);
        string normalized = response.Replace("\r", string.Empty);
        MatchCollection headers = Header().Matches(normalized);
        List<SmsMessage> messages = [];
        for (int i = 0; i < headers.Count; i++)
        {
            Match header = headers[i];
            int bodyStart = header.Index + header.Length;
            int bodyEnd = i + 1 < headers.Count ? headers[i + 1].Index : normalized.IndexOf("\nOK", bodyStart, StringComparison.OrdinalIgnoreCase);
            if (bodyEnd < 0) bodyEnd = normalized.Length;
            string body = normalized[bodyStart..bodyEnd].Trim('\n', ' ', '|');
            _ = int.TryParse(header.Groups[1].Value, out int index);
            messages.Add(new SmsMessage(
                index,
                header.Groups[2].Value,
                header.Groups[3].Value,
                ParseTimestamp(header.Groups[4].Value),
                body));
        }
        SmsMessage[] result = messages.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Index).ToArray();
        App.Log.Info("sms", $"Loaded {result.Length} message(s) from SIM storage.");
        return result;
    }

    public async Task SendAsync(string recipient, string body, CancellationToken cancellationToken = default)
    {
        await at.SendTextMessageAsync(recipient, body, cancellationToken);
        App.Log.Info("sms", "SMS accepted for network delivery.");
    }

    public async Task DeleteAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index is < 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(index));
        string response = await at.ExecuteAsync($"AT+CMGD={index}", TimeSpan.FromSeconds(6), cancellationToken);
        if (!response.Contains("OK", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The modem did not confirm SMS deletion.");
        }
        App.Log.Info("sms", $"Deleted SIM message index {index}.");
    }

    private static DateTimeOffset? ParseTimestamp(string value)
    {
        Match match = Timestamp().Match(value);
        if (!match.Success) return null;
        string date = match.Groups[1].Value;
        return DateTimeOffset.TryParseExact(date, "yy/MM/dd,HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset parsed) ? parsed : null;
    }

    [GeneratedRegex("\\+CMGL:\\s*(\\d+),\"([^\"]+)\",\"([^\"]*)\",(?:\"[^\"]*\",)?\"([^\"]*)\"\\s*\\n", RegexOptions.IgnoreCase)]
    private static partial Regex Header();

    [GeneratedRegex("^(\\d{2}/\\d{2}/\\d{2},\\d{2}:\\d{2}:\\d{2})(?:[+-]\\d{2})?$")]
    private static partial Regex Timestamp();
}
