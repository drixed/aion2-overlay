using AionDpsMeter.Timers.Lobby;

namespace AionDpsMeter.Timers.Tests.Lobby;

/// <summary>EU capture 2026-11-20 16:16: the "Немедленный старт" party of the party finder. 16:16:41 — the list; 16:16:43 I
/// accept (0x9715); 16:16:51 Сиель accepts (0x9716); 16:16:54 the match starts (0x9200); 16:16:55 the list again; 16:17:05 in the
/// dungeon. Frames from the opcode on.</summary>
public class LobbyTrackerTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 16, 16, 41);

    private const string Listed =
        "0297507B0B0021D09DD0B5D0BCD0B5D0B4D0BBD0B5D0BDD0BDD18BD0B920D181D182D0B0D180D18205C42709000003497E020000002205FF0200050C" +
        "01497E0200000022050C63426570547978754279784F050000002D000000EC0B00002205220504FF1C0300000000000001011C02AB65020000002205" +
        "0AD0A1D0B8D0B5D0BBD18C220000002D000000DC0B00002205220504F95D03000000000000F50000000000000001011E0386D3030000002205075354" +
        "4153454353210000002D000000B50A000022052205040A5202000000000000020000000000000001010E04A95A04000000090903687562080000002D" +
        "000000040B00000909090904EB9D0200000000000001010C051E73020000001C05064452495845441A0000002D000000260C00001D051D0504611003" +
        "00000000000001010002";

    private const string AfterAccepts =
        "0297507B0B0021D09DD0B5D0BCD0B5D0B4D0BBD0B5D0BDD0BDD18BD0B920D181D182D0B0D180D18205C42709000003497E020000002205FF0202050C" +
        "01497E0200000022050C63426570547978754279784F050000002D000000EC0B00002205220504FF1C0300000000000001011E02AB65020000002205" +
        "0AD0A1D0B8D0B5D0BBD18C220000002D000000DC0B00002205220504F95D03000000000000F50000000000000001011E0386D3030000002205075354" +
        "4153454353210000002D000000B50A000022052205040A5202000000000000020000000000000001010E04A95A04000000090903687562080000002D" +
        "000000040B00000909090904EB9D0200000000000001010E051E73020000001C05064452495845441A0000002D000000260C0000031D051D05046110" +
        "030000000000000101000A";

    private const string InDungeon =
        "0297507B0B0021D09DD0B5D0BCD0B5D0B4D0BBD0B5D0BDD0BDD18BD0B920D181D182D0B0D180D18205C42709000003497E020000002205FF0202051E" +
        "01497E0200000022050C63426570547978754279784F050000002D000000EC0B00002205EB1004FF1C030000000000001C0200000000000001001E02" +
        "AB650200000022050AD0A1D0B8D0B5D0BBD18C220000002D000000DC0B00002205EB1004F95D03000000000000F50000000000000001001E0386D303" +
        "00000022050753544153454353210000002D000000B50A00002205EB10040A5202000000000000020000000000000001051E04A95A04000000090903" +
        "687562080000002D000000040B0000070909EB1004EB9D020000000000005A0000000000000001001E051E73020000001C05064452495845441A0000" +
        "002D000000260C00001D05EB10046110030000000000004C0000000000000001000009";

    [Fact]
    public void The_party_list_names_every_member_with_server_level_and_who_accepted()
    {
        var party = LobbyParser.Parse(Convert.FromHexString(Listed))!;
        Assert.Equal("Немедленный старт", party.Title);
        Assert.Equal(["cBepTyxuByxO", "Сиель", "STASECS", "hub", "DRIXED"], party.Members.Select(m => m.Name));
        Assert.Equal([1314, 1314, 1314, 2313, 1308], party.Members.Select(m => m.Server));
        Assert.All(party.Members, m => Assert.Equal(45, m.Level));
        Assert.True(party.Members[0].Leader);
        Assert.Equal([false, false, true, true, false], party.Members.Select(m => m.Ready));
    }

    [Fact]
    public void Accepting_sets_the_ready_flag()
    {
        var party = LobbyParser.Parse(Convert.FromHexString(AfterAccepts))!;
        Assert.Equal([false, true, true, true, true], party.Members.Select(m => m.Ready));   // Сиель and I accepted in between
    }

    [Fact]
    public void A_member_accepting_updates_the_list_at_once_and_the_start_closes_it()
    {
        var lobby = new LobbyTracker();
        lobby.OnFrame(T0.AddSeconds(-3), Convert.FromHexString(FinderOpened));
        lobby.OnFrame(T0, Convert.FromHexString(Listed));
        Assert.Equal(T0, lobby.At);
        lobby.OnFrame(T0.AddSeconds(10), Convert.FromHexString("1697AB65020000002205" + "01"));   // Сиель accepts
        Assert.True(lobby.Party!.Members.Single(m => m.Name == "Сиель").Ready);

        lobby.OnFrame(T0.AddSeconds(13), Convert.FromHexString("0092" + "00"));                 // the match starts
        Assert.Null(lobby.Party);
        lobby.OnFrame(T0.AddSeconds(24), Convert.FromHexString(InDungeon));                    // the same party in the dungeon
        Assert.Null(lobby.Party);
    }

    private const string FinderOpened = "0697" + "00000200000000";      // 0x9706, 16:16:38: the party finder's list asked for
    private const string ZoneChanged = "2136" + "01000000C4270900";      // 0x3621, 16:16:58: into the dungeon

    [Fact]
    public void The_party_listed_again_in_the_dungeon_is_not_a_lobby_even_when_the_meter_missed_the_start()
    {
        // Nov 2026: the meter restarted after the match started, so the start frame never came, and in the dungeon the
        // party showed as a lobby with everyone "ждём"
        var lobby = new LobbyTracker();
        lobby.OnFrame(T0, Convert.FromHexString(InDungeon));                                   // no finder used since the start
        Assert.Null(lobby.Party);

        lobby.OnFrame(T0.AddSeconds(1), Convert.FromHexString(FinderOpened));
        lobby.OnFrame(T0.AddSeconds(2), Convert.FromHexString(Listed));
        Assert.NotNull(lobby.Party);
        lobby.OnFrame(T0.AddSeconds(20), Convert.FromHexString(ZoneChanged));                   // a teleport ends the lobby
        Assert.Null(lobby.Party);
        lobby.OnFrame(T0.AddSeconds(27), Convert.FromHexString(InDungeon));
        Assert.Null(lobby.Party);
    }

    /// <summary>16:36: a party made in the finder with the title "200K+", joined by one more player before the start.</summary>
    private const string Party200K =
        "02971B8E0B00053230304B2B05C42709000003B24B03000000FE087F0200050C01B24B03000000FE080B4E49434566616E78696E67080000002D0000" +
        "004C0C0000FE08FE080479740300000000000001020E020F67020000001F05067A6170746571120000002D000000940C00001F051F0504775B030000" +
        "0000000001010C031E73020000001C05064452495845441A0000002D000000260C00001D051D050467100300000000000001010E047DDD0200000025" +
        "050653346E6472301E0000002D000000770B0000170517050378B9020000000000000101000500000000000000000000000000000000000000000003" +
        "00000000000000000000000002";

    private const string Joined =
        "0B970C05A36E03000000FD080C73757065726E61696C6F6E67230000002D000000420C0000010209020903586E030000000000000102";

    [Fact]
    public void A_title_with_signs_still_makes_a_party()
    {
        // v149 showed nothing for this party: "+" was not allowed in a title
        var party = LobbyParser.Parse(Convert.FromHexString(Party200K))!;
        Assert.Equal("200K+", party.Title);
        Assert.Equal("NICEfanxing", party.Members[0].Name);
        Assert.True(party.Members[0].Leader);
        Assert.Contains(party.Members, m => m.Name == "DRIXED");
    }

    [Fact]
    public void A_player_joining_is_added_and_accepting_marks_them()
    {
        var lobby = new LobbyTracker();
        lobby.OnFrame(T0, Convert.FromHexString(FinderOpened));
        lobby.OnFrame(T0.AddSeconds(3), Convert.FromHexString(Party200K));
        var before = lobby.Party!.Members.Count;
        lobby.OnFrame(T0.AddSeconds(50), Convert.FromHexString(Joined));
        var joined = lobby.Party!.Members.Single(m => m.Name == "supernailong");
        Assert.Equal(before + 1, lobby.Party.Members.Count);
        Assert.Equal(5, joined.Slot);
        Assert.False(joined.Ready);
        lobby.OnFrame(T0.AddSeconds(51), Convert.FromHexString("1697A36E03000000FD0801"));
        Assert.True(lobby.Party!.Members.Single(m => m.Name == "supernailong").Ready);
    }

    [Fact]
    public void Other_frames_are_not_a_party()
    {
        Assert.Null(LobbyParser.Parse(Convert.FromHexString("0297507B0B00")));
        Assert.Null(LobbyParser.Parse(Convert.FromHexString("3438000000B9CF01")));
    }
}
