using System;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.BioRand.RE1
{
    internal class Re1DoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds() => new byte[0];

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            ApplyPatches(config, gameData);

            if (!config.RandomDoors)
                return;

            // For RE 1 doors that have 0 as target stage, that means keep the stage
            // the same. This replaces every door with an explicit stage to simplify things
            foreach (var rdt in gameData.Rdts)
            {
                if (!ShouldFixRE1Rdt(config, map, rdt.RdtId))
                    continue;

                foreach (var door in rdt.Doors)
                {
                    var target = door.Target;
                    if (target.Stage == 255)
                        target = new RdtId(rdt.RdtId.Stage, target.Room);
                    door.Target = GetRE1FixedId(map, target);
                }
            }
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
            if (!config.RandomDoors)
                return;

            // Revert the door ID changes we made in begin
            // It probably isn't necessary that we do this, but it seems neater
            foreach (var rdt in gameData.Rdts)
            {
                if (!ShouldFixRE1Rdt(config, map, rdt.RdtId))
                    continue;

                foreach (var door in rdt.Doors)
                {
                    var target = door.Target;
                    if (target.Stage == rdt.RdtId.Stage)
                    {
                        target = new RdtId(255, target.Room);
                        door.Target = target;
                    }
                }
            }
        }

        private bool ShouldFixRE1Rdt(RandoConfig config, Map map, RdtId rdtId)
        {
            var room = map.GetRoom(rdtId);
            if (room == null || room.DoorRando == null)
                return true;

            foreach (var spec in room.DoorRando)
            {
                if (spec.Player != null && spec.Player != config.Player)
                    continue;
                if (spec.Scenario != null && spec.Scenario != config.Scenario)
                    continue;

                if (spec.Category != null)
                {
                    var category = (DoorRandoCategory)Enum.Parse(typeof(DoorRandoCategory), spec.Category, true);
                    if (category == DoorRandoCategory.Exclude)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private RdtId GetRE1FixedId(Map map, RdtId rdtId)
        {
            var rooms = map.Rooms!;
            if (rdtId.Stage == 0 || rdtId.Stage == 1)
            {
                if (!rooms.ContainsKey(rdtId.ToString()))
                    return new RdtId(rdtId.Stage + 5, rdtId.Room);
            }
            else if (rdtId.Stage == 5 || rdtId.Stage == 6)
            {
                if (!rooms.ContainsKey(rdtId.ToString()))
                    return new RdtId(rdtId.Stage - 5, rdtId.Room);
            }
            return rdtId;
        }

        private void ApplyPatches(RandoConfig config, GameData gameData)
        {
            AllowPartnerItemBoxes(gameData);

            if (!config.RandomDoors)
                return;

            ForceTyrant2(gameData);
        }

        private void AllowPartnerItemBoxes(GameData gameData)
        {
            // Remove partner check for these two item boxes
            // This is so Rebecca can use the item boxes
            // Important for Chris 8-inventory because the inventory
            // is now shared for both him and Rebecca and player
            // might need to make space for more items e.g. (V-JOLT)
            var room = gameData.GetRdt(new RdtId(0, 0x00));
            room?.Nop(0x10C92);

            room = gameData.GetRdt(new RdtId(3, 0x03));
            room?.Nop(0x1F920);
        }

        private void ForceTyrant2(GameData gameData)
        {
            var room = gameData.GetRdt(new RdtId(2, 0x03));
            room?.AdditionalOpcodes.Add(new UnknownOpcode(0, 0x05, new byte[] { 0x00, 43, 0 }));
        }
    }
}
