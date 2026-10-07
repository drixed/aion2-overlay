using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Runtime;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.Timers.Bosses;

public sealed class FieldBossListListener(FieldBossTracker tracker, IServerContext server, ILogger<FieldBossListListener> logger)
    : IFieldBossListListener
{
    public void OnFieldBossList(Packet packet)
    {
        // RATmeter frame: varint length prefix, 2 opcode bytes, body.
        if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 2) return;
        var list = FieldBossListParser.Parse(packet.Data.AsSpan(header + 2));
        if (list is null)
        {
            logger.LogDebug("Field boss list not recognised ({Length} bytes)", packet.Data.Length);
            return;
        }
        if (!list.Complete)
            logger.LogWarning("Field boss list for map {Map}: read {Read} of {Count} slots: {Hex}",
                list.MapId, list.Slots.Count, list.Count, Convert.ToHexString(packet.Data));
        tracker.OnList(server.CurrentServerId, list);
    }
}
