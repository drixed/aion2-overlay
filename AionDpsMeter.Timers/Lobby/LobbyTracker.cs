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
    /// <summary>A player joined: <c>0b 97 | member</c>, the member laid out as in the party list.</summary>
    public const ushort JoinedOpcode = 0x970B;
    private const byte Accepted = 0x02;

    /// <param name="frame">The frame from its opcode on.</param>
    public static LobbyParty? Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 8 || frame[0] != 0x02 || frame[1] != 0x97) return null;
        var body = frame[2..];
        long id = BinaryPrimitives.ReadUInt32LittleEndian(body);
        if (!TryText(body, 4, 0, out var title, out var at)) return null;
        long leader = at + 11 <= body.Length ? BinaryPrimitives.ReadUInt32LittleEndian(body[(at + 7)..]) : 0;

        var members = new List<LobbyMember>();
        for (var p = at; p + 11 < body.Length; p++)
        {
            if (!TryMember(body, p, leader, out var member, out var end) || member.Slot != members.Count + 1) continue;
            members.Add(member);
            p = end - 1;
        }
        return members.Count > 0 ? new LobbyParty(id, title, members) : null;
    }

    /// <summary>A player who joined the party (0x970B), or null.</summary>
    public static LobbyMember? Joined(ReadOnlySpan<byte> frame) =>
        frame.Length > 12 && frame[0] == 0x0B && frame[1] == 0x97 && TryMember(frame[2..], 0, 0, out var member, out _) ? member : null;

    /// <summary><c>state u8 | slot u8 | character u32 | 00 00 | server u16 | name | u32 | level u32</c> at <paramref name="p"/>.</summary>
    private static bool TryMember(ReadOnlySpan<byte> body, int p, long leader, out LobbyMember member, out int end)
    {
        member = null!;
        end = p;
        if (p + 11 >= body.Length) return false;
        int slot = body[p + 1];
        if (slot is < 1 or > 8 || body[p + 6] != 0 || body[p + 7] != 0) return false;
        if (!TryText(body, p + 10, 2, out var name, out end) || end + 8 > body.Length) return false;
        long character = BinaryPrimitives.ReadUInt32LittleEndian(body[(p + 2)..]);
        int server = BinaryPrimitives.ReadUInt16LittleEndian(body[(p + 8)..]);
        int level = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[(end + 4)..]);
        member = new LobbyMember(slot, character, server, name, level, (body[p] & Accepted) != 0, character == leader);
        return true;
    }

    /// <summary>Length-prefixed UTF-8 text without control characters ("200K+", "Немедленный старт", names).</summary>
    private static bool TryText(ReadOnlySpan<byte> b, int at, int minLength, out string text, out int end)
    {
        text = string.Empty;
        end = at;
        if (at >= b.Length) return false;
        int length = b[at];
        if (length < minLength || length > 64 || at + 1 + length > b.Length) return false;
        try
        {
            text = new UTF8Encoding(false, true).GetString(b.Slice(at + 1, length));
        }
        catch (ArgumentException) { return false; }
        if (text.Any(char.IsControl) || (minLength > 0 && text.Trim().Length == 0)) return false;
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
        if (opcode is not (LobbyParser.PartyOpcode or LobbyParser.AcceptedOpcode or LobbyParser.StartOpcode or LobbyParser.JoinedOpcode or ZoneOpcode) && !IsFinder(opcode)) return;
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
                case LobbyParser.JoinedOpcode when party is not null:
                    if (LobbyParser.Joined(frame) is not { } joined) return;
                    party = party with { Members = party.Members.Where(m => m.Slot != joined.Slot && m.CharacterId != joined.CharacterId).Append(joined).OrderBy(m => m.Slot).ToList() };
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

/// <summary>
/// Gives party members their names back when the meter missed the character → entity link (sent only on entering a zone,
/// so a restart in the dungeon left "Player_9022"): the game keeps sending every party member's place
/// (<c>1c 92 | character u32 | 00 00 | server u16 | class u32 | zone u32 | u32 | u32 | x f32 | y f32 | z f32 | …</c>), and an
/// unnamed player there alone — within 8 m (the frame comes every two seconds, a runner is further by then), no other unnamed
/// one within twice that — is that character. A place up to two minutes old counts: someone standing still sends no moves.
/// </summary>
public sealed class PartyLinker(
    Func<IEnumerable<(int Entity, bool Named, int? Character)>> players,
    Func<int, AionDpsMeter.Timers.Replay.Fix?> lastPlace,
    Action<int, int> link) : IRawPacketListener
{
    public const ushort Opcode = 0x921C;
    private const float Near = 800, Apart = 1600;
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(2);

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode != Opcode) return;
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
        if (frame.Length < 36 || frame[0] != 0x1C || frame[1] != 0x92 || frame[6] != 0 || frame[7] != 0) return;
        var character = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame[2..]);
        var x = BinaryPrimitives.ReadSingleLittleEndian(frame[26..]);
        var y = BinaryPrimitives.ReadSingleLittleEndian(frame[30..]);
        if (character <= 0 || !float.IsFinite(x) || !float.IsFinite(y)) return;

        var all = players().ToList();
        if (all.Any(p => p.Character == character)) return;
        var near = all.Where(p => !p.Named)
            .Select(p => (p.Entity, Place: lastPlace(p.Entity)))
            .Where(p => p.Place is { } f && (at - f.At).Duration() <= Fresh)
            .Select(p => (p.Entity, Distance: MathF.Sqrt((p.Place!.Value.X - x) * (p.Place.Value.X - x) + (p.Place.Value.Y - y) * (p.Place.Value.Y - y))))
            .OrderBy(p => p.Distance)
            .Take(2)
            .ToList();
        if (near.Count == 0 || near[0].Distance > Near) return;
        if (near.Count > 1 && near[1].Distance < Math.Max(Apart, near[0].Distance * 2)) return;
        link(character, near[0].Entity);
    }
}
