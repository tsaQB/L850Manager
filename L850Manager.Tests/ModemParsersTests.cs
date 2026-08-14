using L850Manager.Services;
using Xunit;

namespace L850Manager.Tests;

public sealed class ModemParsersTests
{
    [Fact]
    public void Xmci_parses_valid_serving_and_neighbor_cells()
    {
        const string response = """
            AT+XMCI=1
            +XMCI: 4,510,11,"0xF4F2","0x0977EC05","0x01A5","0x0000052D","0x00004B7D","0xFFFFFFFF",33,17,0,"0x00000004","0x00000000"
            +XMCI: 5,000,000,"0xFFFE","0xFFFFFFFF","0x017D","0x0000052D","0xFFFFFFFF","0xFFFFFFFF",29,0,255,"0x7FFFFFFF","0x00000000"
            OK
            """;

        var cells = ModemParsers.Xmci(response);

        Assert.Equal(2, cells.Count);
        Assert.True(cells[0].IsServing);
        Assert.Equal(3, cells[0].Band);
        Assert.Equal(1325, cells[0].Earfcn);
        Assert.Equal(421, cells[0].Pci);
        Assert.Equal(-108, cells[0].Rsrp);
        Assert.False(cells[1].IsServing);
        Assert.Equal(381, cells[1].Pci);
    }

    [Theory]
    [InlineData(1325, 3)]
    [InlineData(2450, 5)]
    [InlineData(38650, 40)]
    [InlineData(70546, 0)]
    public void Band_mapping_is_bounded(int earfcn, int expected) =>
        Assert.Equal(expected, ModemParsers.BandFromEarfcn(earfcn));

    [Fact]
    public void Xmci_rejects_out_of_range_pci()
    {
        const string response = "+XMCI: 4,510,11,1,1,504,1325,19325,0,33,17,0,0,0";
        Assert.Empty(ModemParsers.Xmci(response));
    }

    [Fact]
    public void Voltage_accepts_only_plausible_values()
    {
        Assert.Equal(3550, ModemParsers.Voltage("+CBC: 0,3550\r\nOK"));
        Assert.Null(ModemParsers.Voltage("+CBC: 0,99999\r\nOK"));
    }

    [Fact]
    public void Sms_parser_accepts_empty_and_quoted_alpha_fields()
    {
        const string response = "+CMGL: 1,\"REC UNREAD\",\"+620000000001\",,\"26/08/14,21:48:54+28\"\r\nFirst message\r\n+CMGL: 2,\"REC READ\",\"+620000000002\",\"\",\"26/08/14,21:51:03+28\"\r\nSecond message\r\nOK\r\n";

        var messages = SmsParser.ParseTextModeList(response);

        Assert.Equal(2, messages.Count);
        Assert.Equal("Second message", messages[0].Body);
        Assert.Equal(TimeSpan.FromHours(7), messages[0].Timestamp?.Offset);
        Assert.Equal("+620000000001", messages[1].Sender);
        Assert.Equal("REC UNREAD", messages[1].Status);
    }
}
