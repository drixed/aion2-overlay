using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Energy;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class OpcodeOwnershipTests
{
    /// <summary>
    /// Upstream's registry throws at startup when two processors claim one opcode. If upstream ever starts handling
    /// 01 91 itself, this fails in CI before a release is published, instead of the app failing to start.
    /// </summary>
    [Theory]
    [InlineData(FieldBossListParser.Opcode, "FieldBossListForwarder")]
    [InlineData(EnergyParser.Opcode, "EnergyForwarder")]
    public void Only_the_fork_forwarder_handles_its_opcode(ushort opcode, string forwarder)
    {
        var owners = typeof(IFieldBossListListener).Assembly.GetTypes()
            .Where(t => t.CustomAttributes.Any(a =>
                a.AttributeType.Name == "PacketOpcodeAttribute" &&
                a.ConstructorArguments.Count == 1 &&
                Convert.ToUInt16(a.ConstructorArguments[0].Value) == opcode))
            .Select(t => t.Name)
            .ToList();

        Assert.Equal([forwarder], owners);
    }
}
