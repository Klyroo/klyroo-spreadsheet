namespace Klyroo.Spreadsheet.Tests;

// A small .xls assembled by hand from the field tables of MS-CFB and MS-XLS with an independent script (not with the
// writer in XlsFixtures.cs, and not saved by Excel): one 512-byte sector compound file whose Workbook stream (BIFF8, 5
// shared strings, a NUMBER, an RK and a date-formatted NUMBER) lives in the mini stream.
// Sheet "Data": row 1 Name, Qty, Date. Row 2: Cafe (with an accent), 12.5, 45658 as a date. Row 3: an accented word, RK 7.
internal static class XlsGoldenFixture
{
    public static byte[] Bytes() => Convert.FromHexString(Hex);

    private const string Hex =
        "d0cf11e0a1b11ae1000000000000000000000000000000003e000300feff090006000000000000000000000001000000" +
        "0100000000000000001000000200000001000000feffffff0000000000000000ffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffdfffffffefffffffefffffffeffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffff52006f006f007400200045006e00740072007900000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000016000501ffffffffffffffff01000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000030000004001000000000000" +
        "57006f0072006b0062006f006f006b000000000000000000000000000000000000000000000000000000000000000000" +
        "0000000000000000000000000000000012000201ffffffffffffffffffffffff00000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000270100000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "02000001ffffffffffffffffffffffff0000000000000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000002000001ffffffffffffffffffffffff" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "01000000020000000300000004000000feffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" +
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff0908100000060500bb0dcc0700000000" +
        "06000000e00014000000000000000000000000000000000000000000e000140000000e00000000000000000000000000" +
        "0000000085000c00850000000000040044617461fc00290005000000050000000400004e616d65030000517479040000" +
        "44617465040000436166e9030000dc6eef0a0000000908100000061000bb0dcc07000000000600000000020e00000000" +
        "0003000000000003000000fd000a0000000000000000000000fd000a0000000100000001000000fd000a000000020000" +
        "0002000000fd000a000100000000000300000003020e00010001000000000000000000294003020e0001000200010000" +
        "000000404be640fd000a00020000000000040000007e020a000200010000001e0000000a000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000" +
        "00000000000000000000000000000000";
}
