namespace AionDpsMeter.Core.Data;

public static class ServerMap
{
	public static readonly Dictionary<int, string> Servers = new Dictionary<int, string>
	{
		[2001] = "ISR",
		[2002] = "SIE",
		[2003] = "TRI",
		[2004] = "LUM",
		[2005] = "MAR",
		[2006] = "AZP",
		[2007] = "ERE",
		[2008] = "BER",
		[2009] = "NEM",
		[2010] = "HAD",
		[2011] = "LUD",
		[2012] = "ULG",
		[2013] = "MUN",
		[2014] = "ODA",
		[2015] = "ZEN",
		[2016] = "KRO",
		[2017] = "KUI",
		[2018] = "BAB",
		[2019] = "FAF",
		[2020] = "IND",
		[2021] = "ISH",
		[1001] = "SIE",
		[1002] = "NEZ",
		[1003] = "VAI",
		[1004] = "KAI",
		[1005] = "YUS",
		[1006] = "ARI",
		[1007] = "FRE",
		[1008] = "MES",
		[1009] = "HIT",
		[1010] = "NAN",
		[1011] = "TAH",
		[1012] = "LUT",
		[1013] = "PER",
		[1014] = "DAM",
		[1015] = "KAS",
		[1016] = "BAK",
		[1017] = "CHE",
		[1018] = "KOS",
		[1019] = "ISH",
		[1020] = "TIA",
		[1021] = "POE",
		// aion2-overlay fork: Europe (ids 13xx Elyos, 23xx Asmodians; names from the game's ServerName_<id> text as listed by
		// SkeeveAN/Aion-DPS-Meter, MIT). EU players had no server name, so boss timers could not be kept per server.
		[1301] = "SIE",
		[1302] = "NEZ",
		[1303] = "VAI",
		[1304] = "KAI",
		[1305] = "YUS",
		[1306] = "ARI",
		[1307] = "FRE",
		[1308] = "MES",
		[1309] = "HIT",
		[1310] = "NAN",
		[1311] = "TAH",
		[1312] = "LUT",
		[1313] = "PHE",
		[1314] = "DAM",
		[1315] = "KAS",
		[1316] = "BAK",
		[1317] = "TSE",
		[1318] = "KOC",
		[1319] = "ISH",
		[1320] = "TIA",
		[1321] = "GAU",
		[1322] = "LAM",
		[1323] = "NAT",
		[2301] = "ISR",
		[2302] = "ZIK",
		[2303] = "TRI",
		[2304] = "LUM",
		[2305] = "MAR",
		[2306] = "AZP",
		[2307] = "ERE",
		[2308] = "BER",
		[2309] = "NEM",
		[2310] = "HAD",
		[2311] = "LUD",
		[2312] = "ULG",
		[2313] = "MUN",
		[2314] = "ODA",
		[2315] = "ZEM",
		[2316] = "KRO",
		[2317] = "QUA",
		[2318] = "BAB",
		[2319] = "FAF",
		[2320] = "IND",
		[2321] = "AGN",
		[2322] = "ATI",
		[2323] = "TAS"
	};

	public static string GetName(int id)
	{
		if (!Servers.TryGetValue(id, out string value))
		{
			return "";
		}
		return value;
	}

	public static bool IsValidId(int id) => Servers.ContainsKey(id);
}
