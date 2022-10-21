using System;
using System.IO;

namespace rer
{
    internal class BioScriptVisitor
    {
        public virtual void VisitBeginScript(BioScriptKind kind)
        {
        }

        public virtual void VisitEndScript(BioScriptKind kind)
        {
        }

        public virtual void VisitBeginSubroutine(int index)
        {
        }

        public virtual void VisitEndSubroutine(int index)
        {
        }

        public virtual void VisitOpcode(int offset, Opcode opcode, Span<byte> operands)
        {
            VisitOpcode(offset, opcode, new BinaryReader(new MemoryStream(operands.ToArray())));
        }

        private void VisitOpcode(int offset, Opcode opcode, BinaryReader br)
        {
            switch (opcode)
            {
                case Opcode.DoorAotSe:
                    VisitDoorAotSe(new Door()
                    {
                        Offset = offset,
                        Opcode = (byte)opcode,
                        Id = br.ReadByte(),
                        Unknown2 = br.ReadUInt16(),
                        Unknown4 = br.ReadUInt16(),
                        X = br.ReadInt16(),
                        Y = br.ReadInt16(),
                        W = br.ReadInt16(),
                        H = br.ReadInt16(),
                        NextX = br.ReadInt16(),
                        NextY = br.ReadInt16(),
                        NextZ = br.ReadInt16(),
                        NextD = br.ReadInt16(),
                        Stage = br.ReadByte(),
                        Room = br.ReadByte(),
                        Camera = br.ReadByte(),
                        Unknown19 = br.ReadByte(),
                        DoorType = br.ReadByte(),
                        DoorFlag = br.ReadByte(),
                        Unknown1C = br.ReadByte(),
                        DoorLockFlag = br.ReadByte(),
                        DoorKey = br.ReadByte(),
                        Unknown1F = br.ReadByte()
                    });
                    break;
                case Opcode.SceEmSet:
                    VisitSceEmSet(new RdtEnemy()
                    {
                        Offset = offset,
                        Opcode = (byte)opcode,
                        Unk01 = br.ReadByte(),
                        Id = br.ReadByte(),
                        Type = (EnemyType)br.ReadByte(),
                        State = br.ReadByte(),
                        Ai = br.ReadByte(),
                        Floor = br.ReadByte(),
                        SoundBank = br.ReadByte(),
                        Texture = br.ReadByte(),
                        KillId = br.ReadByte(),
                        X = br.ReadInt16(),
                        Y = br.ReadInt16(),
                        Z = br.ReadInt16(),
                        D = br.ReadInt16(),
                        Animation = br.ReadUInt16(),
                        Unk15 = br.ReadByte(),
                    });
                    break;
                case Opcode.AotReset:
                    VisitAotReset(new Reset()
                    {
                        Offset = offset,
                        Opcode = (byte)opcode,
                        Id = br.ReadByte(),
                        Unk2 = br.ReadUInt16(),
                        Type = br.ReadUInt16(),
                        Amount = br.ReadUInt16(),
                        Unk8 = br.ReadUInt16(),
                    });
                    break;
                case Opcode.ItemAotSet:
                    VisitItemAotSet(new Item()
                    {
                        Offset = offset,
                        Opcode = (byte)opcode,
                        Id = br.ReadByte(),
                        Unknown0 = br.ReadInt32(),
                        X = br.ReadInt16(),
                        Y = br.ReadInt16(),
                        W = br.ReadInt16(),
                        H = br.ReadInt16(),
                        Type = br.ReadUInt16(),
                        Amount = br.ReadUInt16(),
                        Array8Idx = br.ReadUInt16(),
                        Unknown1 = br.ReadUInt16(),
                    });
                    break;
                case Opcode.XaOn:
                    VisitXaOn(new RdtSound()
                    {
                        Offset = offset,
                        Opcode = (byte)opcode,
                        Channel = br.ReadByte(),
                        Id = br.ReadUInt16()
                    });
                    break;
            }
        }

        protected virtual void VisitDoorAotSe(Door door)
        {
        }

        protected virtual void VisitSceEmSet(RdtEnemy enemy)
        {
        }

        protected virtual void VisitAotReset(Reset reset)
        {
        }

        protected virtual void VisitItemAotSet(Item item)
        {
        }

        protected virtual void VisitXaOn(RdtSound sound)
        {
        }
    }

    internal enum BioScriptKind
    {
        Init,
        Main
    }
}
