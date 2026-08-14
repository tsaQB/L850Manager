using System.Globalization;
using System.Text.RegularExpressions;
using L850Manager.Models;

namespace L850Manager.Services;

public static partial class ModemParsers
{
    public static string QuotedValue(string response, string prefix)
    {
        Match match = Regex.Match(response, Regex.Escape(prefix) + "\\s*:\\s*\"?([^\"\\r\\n]+)\"?", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim().Trim('"') : "—";
    }

    public static int? Voltage(string response)
    {
        Match match = Regex.Match(response, @"\+CBC:\s*\d+\s*,\s*(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int value) && value is >= 2500 and <= 5000 ? value : null;
    }

    public static IReadOnlyList<CellSnapshot> Xmci(string response)
    {
        List<CellSnapshot> cells = [];
        foreach (Match match in XmciLine().Matches(response))
        {
            string[] fields = match.Groups[1].Value.Split(',').Select(x => x.Trim().Trim('"')).ToArray();
            if (fields.Length != 14 || !TryNumber(fields[0], out int type) || type is not (4 or 5))
            {
                continue;
            }
            if (!TryNumber(fields[5], out int pci) || !TryNumber(fields[6], out int earfcn) || pci is < 0 or > 503)
            {
                continue;
            }
            int band = BandFromEarfcn(earfcn);
            if (band == 0)
            {
                continue;
            }
            double? rsrp = TryNumber(fields[9], out int rsrpRaw) && rsrpRaw != 255 ? rsrpRaw - 141d : null;
            double? rsrq = TryNumber(fields[10], out int rsrqRaw) && rsrqRaw != 255 ? (rsrqRaw / 2d) - 20d : null;
            double? sinr = TryNumber(fields[11], out int sinrRaw) && sinrRaw is not (127 or 255) ? sinrRaw : null;
            cells.Add(new CellSnapshot(type == 4, band, earfcn, pci, rsrp, rsrq, sinr));
        }
        return cells;
    }

    public static int ActiveCarrierCount(string response)
    {
        Match match = Regex.Match(response, @"\+GTCAINFO:\s*(\d+)\s*(?:\r?\n|$)");
        if (!match.Success)
        {
            match = Regex.Match(response, @"\+GTCAINFO:\s*(\d+)\s+\+GTCAINFO:");
        }
        return match.Success && int.TryParse(match.Groups[1].Value, out int count) ? Math.Max(1, count - 1) : 1;
    }

    public static int BandFromEarfcn(int earfcn) => earfcn switch
    {
        >= 0 and <= 599 => 1,
        >= 600 and <= 1199 => 2,
        >= 1200 and <= 1949 => 3,
        >= 1950 and <= 2399 => 4,
        >= 2400 and <= 2649 => 5,
        >= 2750 and <= 3449 => 7,
        >= 3450 and <= 3799 => 8,
        >= 6150 and <= 6449 => 20,
        >= 9210 and <= 9659 => 28,
        >= 38650 and <= 39649 => 40,
        >= 39650 and <= 41589 => 41,
        _ => 0
    };

    private static bool TryNumber(string input, out int value)
    {
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(input[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    [GeneratedRegex(@"\+XMCI:\s*([^\r\n]+)", RegexOptions.IgnoreCase)]
    private static partial Regex XmciLine();
}
