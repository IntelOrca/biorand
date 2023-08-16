using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.BioRand.RE2
{
    internal class Re2DoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds() => new byte[0];

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            Nop(0x10B, 0x1756, 0x175E);
            Nop(0x10C, 0x1AB4);
            Nop(0x20B, 0x3762, 0x3754);
            Nop(0x20D, 0x113C);
            Nop(0x20F, 0x182A, 0x182A);
            Nop(0x103, 0x36FE);
            Nop(0x108, 0x1B0A);
            Nop(0x118, 0x1EF6);
            Nop(0x208, 0x14EC, 0x14F4);
            Nop(0x210, 0x135E, 0x12E8);
            Nop(0x211, 0x177E);
            Nop(0x408, 0x2974);
            Nop(0x615, 0x2FD8, 0x3562);

            void Nop(ushort rdtId, int address0, int address1 = -1)
            {
                var rdt = gameData.GetRdt(RdtId.FromInteger(rdtId));
                if (config.Player == 0)
                    rdt?.Nop(address0);
                else
                    rdt?.Nop(address1 == -1 ? address0 : address1);
            }
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
            // See https://github.com/IntelOrca/biorand/issues/265
            var rdt = gameData.GetRdt(new RdtId(0, 0x0D));
            if (rdt == null || rdt.Version != BioVersion.Biohazard2 || config.Player != 1)
                return;

            rdt.Nop(0x0894);
            rdt.Nop(0x08BA);
        }

        public string? GetRoomDisplayName(RdtId id)
        {
            var map = _roomDisplayNameMap.Value;
            if (map.TryGetValue(id, out var value))
                return value;

            return null;
        }

        private static Lazy<Dictionary<RdtId, string>> _roomDisplayNameMap = new Lazy<Dictionary<RdtId, string>>(() =>
        {
            var map = new Dictionary<RdtId, string>();
            foreach (var s in _roomDisplayNames!)
            {
                var parts = s.Split(':');
                var rtdId = RdtId.Parse(parts[0]);
                var name = parts[1];
                map[rtdId] = name;
            }
            return map;
        });

        private readonly static string[] _roomDisplayNames = new string[]
        {
            "100:Start (Scenario A)",
            "101:Kendo's Gun Shop",
            "102:Basketball Court",
            "103:RPD Front",
            "104:Start (Scenario B)",
            "105:RPD Back Car Park",
            "106:RPD Cabin",
            "107:RPD Cabin Street",
            "108:RPD Helipad",
            "109:RPD 2F Back Entrance",
            "10A:RPD 2F Helicopter Fire",
            "10B:RPD 2F Treasure Vault",
            "10C:RPD 2F Statue Corridor",
            "10D:RPD 2F Chief's Corridor",
            "10E:RPD 2F Chief's Gallery",
            "10F:RPD 2F Save Room",
            "110:RPD 2F Hall",
            "111:RPD 3F Clock Tower",
            "112:RPD 2F Library",
            "113:RPD 2F T Corridor",
            "114:RPD 2F S.T.A.R.S. Corridor",
            "115:RPD 2F S.T.A.R.S. Office",
            "116:RPD 2F East Stairs",
            "117:RPD 3F Hall",
            "118:Alley",
            "119:Streets",
            "11A:Inside Bus",
            "11B:Streets 2",
            "11C:RPD Helipad (4th Survivor)",
            "11D:Alley (RE2 Trial)",
            "200:RPD 1F Hall",
            "201:RPD 1F West Waiting Room",
            "202:RPD 1F West Office",
            "203:RPD 1F Records Room",
            "204:RPD 1F Licker Corridor",
            "205:RPD 1F Arms Corridor",
            "206:RPD 1F Operations Room",
            "207:RPD 1F West Stairs",
            "208:RPD 1F Dark Room",
            "209:RPD 1F Evidence Room",
            "20A:RPD 1F East Waiting Room",
            "20B:RPD 1F East Office",
            "20C:RPD 1F East L Corridor",
            "20D:RPD 1F Press Room",
            "20E:RPD 1F Interrogation Room",
            "20F:RPD 1F East Basement Corridor",
            "210:RPD 1F Night Duty Room",
            "211:RPD B1 Basement",
            "212:RPD B1 Alley",
            "213:RPD B1 Boiler Room",
            "214:RPD B1 Weapon Storage",
            "215:RPD B1 Morgue",
            "216:RPD B1 Car Park",
            "217:???",
            "218:???",
            "219:RPD B1 Detention Corridor",
            "21A:RPD B1 Kennel",
            "21B:RPD 2F Chief's Office",
            "300:RPD B1 Chief's Dungeon",
            "301:RPD B1 Detention Area",
            "302:Sewage Disposal Leon Passage",
            "303:Sewage Disposal Leon Boss",
            "304:Sewage Disposal Leon Exit",
            "305:Sewage Disposal Pool",
            "306:Sewage Disposal Box Room",
            "307:Sewage Disposal Construction Site",
            "308:B1 Chief's Secret Passage",
            "309:Sewage Disposal Claire Boss",
            "30A:Sewage Disposal Claire Exit",
            "30B:Sewage Disposal Leon Corridor",
            "30C:Sewage Disposal West Save Room",
            "30D:Sewage Disposal East Save Room",
            "400:Sewer B1 Fork Tunnel",
            "401:Sewer B1 South Office",
            "402:Sewer B2 L-Passage",
            "403:Sewer B1 North Office",
            "404:Sewer B2 T-Passage",
            "405:Sewer B2 Canal A",
            "406:",
            "407:Sewer B2 Canal B",
            "408:Sewer B2 Bridge",
            "409:Sewer B1 Bridge",
            "40A:Sewer B2 Alligator Tunnel",
            "40B:Sewer B1 Hidden Tunnel",
            "40C:Sewer B1 Dump Gathering",
            "40D:Sewer B2 Cave",
            "40E:Sewer B2 Tram",
            "40F:Sewer B2 Vent",
            "410:Sewer B2 Alligator Pool",
            "500:Factory Tram",
            "501:Factory Tram Platform",
            "502:Factory Corridor 1",
            "503:Factory Panel Room",
            "504:Factory Turntable (Up)",
            "505:Factory Inside Turntable",
            "506:Factory Turntable (Down)",
            "507:Factory Turntable Control Room",
            "508:Factory Corridor 2",
            "509:Factory Turntable Descend",
            "600:Lab B1 Turntable",
            "601:Lab B4 Elevator",
            "602:Lab B1 Corridor",
            "603:Lab B1 Pump Room",
            "604:Lab B1 Power Room",
            "605:Lab B3 Power Room",
            "606:Lab B4 Turntable",
            "607:Lab B4 Security Office",
            "608:Lab B4 Shaft",
            "609:Lab B4 Private Quarters A",
            "60A:Lab B4 Private Quarters B",
            "60B:Lab B4 Left Corridor",
            "60C:Lab B4 Right Corridor",
            "60D:Lab B4 Frozen Room",
            "60E:Lab B4 Ladder",
            "60F:Lab B4 G-Capsule Room",
            "610:Lab B4 CT Scan Room",
            "611:Lab B4 Shaft (Leon)",
            "612:Lab B5 Control Room",
            "613:Lab B5 Server Room",
            "614:Lab B5 T-Passage",
            "615:Lab B5 Laboratory",
            "616:Lab B5 MO Disk Corridor",
            "617:Lab B5 G Boss",
            "700:Train Platform Entrance",
            "701:Train Platform",
            "702:Train Tyrant Boss",
            "703:Train",
            "704:Train Storage",
        };
    }
}
