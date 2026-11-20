// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;
using System.Text;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Lobby;

public sealed record LobbyMember(int Slot, long CharacterId, int Server, string Name, int Level, bool Ready, bool Leader);

public sealed record LobbyParty(long Id, string Title, IReadOnlyList<LobbyMember> Members);

/// <summary>
/// The party of the party finder ("Немедленный старт"), as the game sends it (EU capture 2026-11-20):
/// <c>02 97 | party id u32 | title (u8 length + UTF-8) | count u8 | dungeon u32 | 2 bytes | leader u32 | … | members</c>,
/// each member <c>state u8 | slot u8 | character u32 | 00 00 | server u16 | name (u8 length + UTF-8) | u32 | level u32 | …</c>.
/// State bit 0x02 is "accepted": it appeared for Сиель and for me right after each of us accepted.
/// </summary>
public static class LobbyParser
{
    public const ushort PartyOpcode = 0x9702;
    public const ushort AcceptedOpcode = 0x9716;
    public const ushort StartOpcode = 0x9200;
    private const byte Accepted = 0x02;

    /// <param name="frame">The frame from its opcode on.</param>
    public static LobbyParty? Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 8 || frame[0] != 0x02 || frame[1] != 0x97) return null;
        var body = frame[2..];
        long id = BinaryPrimitives.ReadUInt32LittleEndian(body);
        if (!TryText(body, 4, out var title, out var at)) return null;
        long leader = at + 11 <= body.Length ? BinaryPrimitives.ReadUInt32LittleEndian(body[(at + 7)..]) : 0;

        var members = new List<LobbyMember>();
        for (var p = at; p + 11 < body.Length; p++)
        {
            int slot = body[p + 1];
            if (slot is < 1 or > 8 || slot != members.Count + 1 || body[p + 6] != 0 || body[p + 7] != 0) continue;
            if (!TryText(body, p + 10, out var name, out var end) || end + 8 > body.Length) continue;
            long character = BinaryPrimitives.ReadUInt32LittleEndian(body[(p + 2)..]);
            int server = BinaryPrimitives.ReadUInt16LittleEndian(body[(p + 8)..]);
            int level = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[(end + 4)..]);
            members.Add(new LobbyMember(slot, character, server, name, level, (body[p] & Accepted) != 0, character == leader));
            p = end - 1;
        }
        return members.Count > 0 ? new LobbyParty(id, title, members) : null;
    }

    private static bool TryText(ReadOnlySpan<byte> b, int at, out string text, out int end)
    {
        text = string.Empty;
        end = at;
        if (at >= b.Length) return false;
        int length = b[at];
        if (length is < 1 or > 48 || at + 1 + length > b.Length) return false;
        try
        {
            text = new UTF8Encoding(false, true).GetString(b.Slice(at + 1, length));
        }
        catch (ArgumentException) { return false; }
        if (!text.All(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-' or '.' or '!' or '?')) return false;
        end = at + 1 + length;
        return true;
    }
}

/// <summary>
/// The party finder's party while it gathers: who is in it and who accepted. A member accepting comes at once
/// (<c>16 97 | character u32 | 00 00 | server u16 | 01</c>); the match starting (0x9200) or a zone change (0x3621) closes it.
/// A party list counts only after the party finder was used (0x9701/06/0A/15/16) since the last zone change: in the
/// dungeon the game lists the same party again, and a meter started after the match never saw it start.
/// </summary>
public sealed class LobbyTracker : IRawPacketListener
{
    private readonly Lock gate = new();
    private readonly HashSet<long> started = [];
    private const ushort ZoneOpcode = 0x3621;
    private LobbyParty? party;
    private bool finderUsed;

    /// <summary>The party finder's own frames: its list, applying, accepting.</summary>
    private static bool IsFinder(ushort opcode) => opcode is 0x9701 or 0x9706 or 0x970A or 0x9715 or LobbyParser.AcceptedOpcode;

    public LobbyParty? Party { get { lock (gate) return party; } }

    /// <summary>When the party last changed.</summary>
    public DateTime? At { get; private set; }

    public event Action? Changed;

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode is not (LobbyParser.PartyOpcode or LobbyParser.AcceptedOpcode or LobbyParser.StartOpcode or ZoneOpcode) && !IsFinder(opcode)) return;
        try
        {
            if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 3) return;
            OnFrame(DateTime.Now, packet.Data.AsSpan(header));
        }
        catch (Exception) { }
    }

    /// <param name="frame">The frame from its opcode on.</param>
    public void OnFrame(DateTime at, ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2) return;
        var opcode = (ushort)(frame[0] | frame[1] << 8);
        lock (gate)
        {
            if (IsFinder(opcode)) finderUsed = true;
            switch (opcode)
            {
                case ZoneOpcode:
                    finderUsed = false;
                    if (party is null) return;
                    party = null;
                    break;
                case LobbyParser.PartyOpcode:
                    if (!finderUsed || LobbyParser.Parse(frame) is not { } next || started.Contains(next.Id)) return;
                    party = next;
                    break;
                case LobbyParser.AcceptedOpcode when party is not null && frame.Length >= 11:
                    long character = BinaryPrimitives.ReadUInt32LittleEndian(frame[2..]);
                    var ready = frame[10] == 1;
                    if (!party.Members.Any(m => m.CharacterId == character)) return;
                    party = party with { Members = party.Members.Select(m => m.CharacterId == character ? m with { Ready = ready } : m).ToList() };
                    break;
                case LobbyParser.StartOpcode when party is not null:
                    started.Add(party.Id);
                    party = null;
                    break;
                default:
                    return;
            }
            At = at;
        }
        Changed?.Invoke();
    }
}
