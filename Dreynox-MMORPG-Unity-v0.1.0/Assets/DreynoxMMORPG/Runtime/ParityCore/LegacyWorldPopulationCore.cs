using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.ParityCore
{
    public readonly struct LegacyWorldPopulation
    {
        public readonly int MapId;
        public readonly int Portals;
        public readonly int Npcs;
        public readonly int MobAreas;
        public readonly int Mobs;
        public readonly int Bosses;
        public readonly int Obelisks;

        public LegacyWorldPopulation(
            int mapId,
            int portals,
            int npcs,
            int mobAreas,
            int mobs,
            int bosses,
            int obelisks)
        {
            MapId = mapId;
            Portals = portals;
            Npcs = npcs;
            MobAreas = mobAreas;
            Mobs = mobs;
            Bosses = bosses;
            Obelisks = obelisks;
        }
    }

    public static class LegacyWorldPopulationCore
    {
        public const int LoginPort = 30800;
        public const string OfflineMode =
            "native ps0032 original; loopback only";

        private static readonly LegacyWorldPopulation[] Maps =
        {
            P(0, 11, 141, 509, 1330, 0, 1),
            P(1, 11, 247, 502, 1186, 0, 0),
            P(2, 11, 194, 800, 868, 0, 0),
            P(18, 2, 72, 221, 452, 0, 1),
            P(19, 9, 128, 622, 1208, 0, 0),
            P(20, 8, 178, 601, 1236, 0, 0),
            P(29, 3, 137, 579, 1107, 0, 0),
            P(35, 3, 155, 0, 0, 0, 0),
            P(36, 5, 155, 0, 0, 0, 0),
            P(40, 0, 2, 0, 0, 0, 0),
            P(42, 3, 47, 0, 0, 0, 0),
            P(53, 4, 5, 0, 0, 0, 0),
            P(54, 5, 5, 0, 0, 0, 0),
        };

        private static readonly Dictionary<int, LegacyWorldPopulation> Lookup =
            BuildLookup();

        public static IReadOnlyList<LegacyWorldPopulation> All => Maps;

        public static bool TryGet(
            int mapId,
            out LegacyWorldPopulation population)
        {
            return Lookup.TryGetValue(mapId, out population);
        }

        public static LegacyWorldPopulation Get(int mapId)
        {
            LegacyWorldPopulation value;
            if (!TryGet(mapId, out value))
                throw new ArgumentOutOfRangeException(
                    nameof(mapId),
                    "Map has no captured offline ps0032 population baseline.");
            return value;
        }

        private static LegacyWorldPopulation P(
            int mapId,
            int portals,
            int npcs,
            int mobAreas,
            int mobs,
            int bosses,
            int obelisks)
        {
            return new LegacyWorldPopulation(
                mapId,
                portals,
                npcs,
                mobAreas,
                mobs,
                bosses,
                obelisks);
        }

        private static Dictionary<int, LegacyWorldPopulation> BuildLookup()
        {
            var result = new Dictionary<int, LegacyWorldPopulation>();
            for (int i = 0; i < Maps.Length; i++)
            {
                if (result.ContainsKey(Maps[i].MapId))
                    throw new InvalidOperationException(
                        "Duplicate map baseline " + Maps[i].MapId + ".");
                result.Add(Maps[i].MapId, Maps[i]);
            }
            return result;
        }
    }
}
