// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Pets;

/// <summary>
/// What the character has selected: the game sends <c>34 38 | 00 00 00 | target (varint)</c> on every click on a monster
/// and the same with a zero target when the selection is cleared (EU capture 2026-11-20: five clicks on one monster with
/// Esc in between, then on another). Lets the pet line show a monster's pet before the first hit.
/// </summary>
public sealed class TargetSelection : IRawPacketListener
{
    private const ushort Opcode = 0x3834;
    private volatile Selected? current;

    private sealed record Selected(int Entity, DateTime At);

    /// <summary>The selected entity, null when nothing is selected.</summary>
    public int? Entity => current?.Entity;

    /// <summary>When it was selected.</summary>
    public DateTime? At => current?.At;

    public event Action? Changed;

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
        if (frame.Length < 6 || frame[0] != 0x34 || frame[1] != 0x38) return;
        if (!Wire.TryVarint(frame, 5, out var target, out _)) return;
        var next = target is > 0 and <= int.MaxValue ? new Selected((int)target, at) : null;
        if (next?.Entity == current?.Entity && next is not null) return;
        current = next;
        Changed?.Invoke();
    }
}
