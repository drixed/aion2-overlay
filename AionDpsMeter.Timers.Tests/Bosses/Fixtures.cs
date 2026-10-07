namespace AionDpsMeter.Timers.Tests.Bosses;

/// <summary>Live EU packets from the tests of cyberbadger6969/aion2-dps-meter (GPL-3.0).</summary>
internal static class Fixtures
{
    /// <summary>Altgard (map 1110), 24 field bosses, captured on EU 2026-10-05/06. Body after the opcode.</summary>
    public const string AltgardList =
        "00005604000018009de30602729d660ea10100000199e30691939bc7645092c700ecdd467f04f30da1010000009fe3066d013f0ea1010000" +
        "00a0e3066efe360ea1010000009ae306c4e9010ea1010000009be306a54a140ea1010000009ee306bc74360ea1010000009ce30630852c0e" +
        "a101000001a1e30600a57447800a0dc800f0db45095a05d40da101000000a2e3061f365b0ea101000000a3e306d4eb670ea101000001a4e3" +
        "06413f42482dc5e5c700b6974683a4f40da101000000ace30650d2de0ea101000000a5e3068054070ea101000000a6e306dae65e0ea10100" +
        "0000a7e3061737050ea101000000a8e306001166e80ea101000000a9e306ef5f4d0ea101000000aae30668a8020ea101000000abe306c284" +
        "d30ea101000000ade306fbccc10ea101000000aee3061cfdc60ea101000000afe306bf00d70ea101000000b0e3063501d10ea10100000000" +
        "00";

    /// <summary>Map 20, 8 scheduled bosses (2026-10-06 01:07): slot 2001 carries the extra byte, slot 2002 has no time.</summary>
    public const string List20 =
        "000014000000080" + "0d10f00f187f20ea1010000" + "00d20f0000000000000000" + "00d30f60eb0d22a1010000" +
        "00d40f60eb0d22a1010000" + "00d50f60eb0d22a1010000" + "00d60fa006031da1010000" + "00d80fa006031da1010000" +
        "00d70fa006031da1010000" + "000000";

    public static byte[] Bytes(string hex) => Convert.FromHexString(hex);
}
