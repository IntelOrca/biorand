using System.IO;
using IntelOrca.Biohazard.Room;

namespace IntelOrca.Biohazard.BioRand.RECV
{
    internal class ReCvDoorHelper : IDoorHelper
    {
        public byte[] GetReservedLockIds() => new byte[0];

        public void Begin(RandoConfig config, GameData gameData, Map map)
        {
            Nop(gameData, new RdtId(0x00, 0x07), 0x1819AE); // Force window cutscene on item interaction
        }

        private void OverrideItemPickup(RandomizedRdt rrdt, byte aotIndex, byte itemType, byte globalId)
        {
            var rdtBuilder = ((RdtCv)rrdt.RdtFile).ToBuilder();

            // Convert AOT to a message
            var aot = rdtBuilder.Aots[aotIndex];
            aot.Kind = 3;
            aot.Flags = 0;
            rdtBuilder.Aots[aotIndex] = aot;

            var scriptBuilder = rdtBuilder.Script.ToBuilder();
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            // Write original procedure code
            var originalData = scriptBuilder.Procedures[1].Data;
            bw.Write(originalData.Slice(0, originalData.Length - 2));

            // Write item check code
            bw.Write(GetItemPickupCodes(aotIndex, aot.Stage, itemType, globalId));

            // Write new end opcode
            bw.Write((byte)0);
            bw.Write((byte)0);

            scriptBuilder.Procedures[1] = new ScdProcedure(BioVersion.BiohazardCv, ms.ToArray());
            rdtBuilder.Script = scriptBuilder.ToProcedureList();
            rrdt.RdtFile = rdtBuilder.ToRdt();
        }

        private void Nop(GameData gameData, RdtId rtdId, int offset)
        {
            var rrdt = gameData.GetRdt(rtdId);
            if (rrdt == null)
                return;

            rrdt.Nop(offset);
        }

        private void Nop(GameData gameData, RdtId rtdId, int beginOffset, int endOffset)
        {
            var rrdt = gameData.GetRdt(rtdId);
            if (rrdt == null)
                return;

            rrdt.Nop(beginOffset, endOffset);
        }

        private byte[] GetItemPickupCodes(byte aotIndex, byte itemIndex, byte itemType, byte globalId)
        {
            return new byte[]
            {
                0x01, 0x2C,
                0x04, 0x07, globalId, 0x00, 0x00, 0x01,
                0x25, aotIndex, 0x00, 0x00, itemIndex, 0x00, 0x00, 0x00, 0x03, 0x00,
                0x01, 0x18,
                0x04, 0x0A, 0x17, 0x00, aotIndex, 0x00,
                0x05, 0x0A, 0x1B, 0x00, 0x00, 0x00,
                0x08, 0x08, itemType, 0x00,
                0x05, 0x0A, 0x17, 0x00, aotIndex, 0x01,
                0x03, 0x00,
                0x02, 0x0C,
                0x25, aotIndex, 0x00, 0x80, 0x00, 0x05, 0x00, 0x00, 0x03, 0x00,
            };
        }

        public void End(RandoConfig config, GameData gameData, Map map)
        {
        }
    }
}
