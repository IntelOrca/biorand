using System;
using System.IO;
using rer.Opcodes;

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
            VisitOpcode(offset, 1 + operands.Length, opcode, new BinaryReader(new MemoryStream(operands.ToArray())));
        }

        private void VisitOpcode(int offset, int length, Opcode opcode, BinaryReader br)
        {
            switch (opcode)
            {
                default:
                    VisitUnknownOpcode(UnknownOpcode.Read(br, opcode, offset, length));
                    break;
                case Opcode.DoorAotSe:
                    VisitDoorAotSe(DoorAotSeOpcode.Read(br, offset));
                    break;
                case Opcode.SceEmSet:
                    VisitSceEmSet(SceEmSetOpcode.Read(br, offset));
                    break;
                case Opcode.AotReset:
                    VisitAotReset(AotResetOpcode.Read(br, offset));
                    break;
                case Opcode.ItemAotSet:
                    VisitItemAotSet(ItemAotSetOpcode.Read(br, offset));
                    break;
                case Opcode.XaOn:
                    VisitXaOn(XaOnOpcode.Read(br, offset));
                    break;
                case Opcode.SceItemGet:
                    VisitSceItemGet(SceItemGetOpcode.Read(br, offset));
                    break;
            }
        }

        protected virtual void VisitUnknownOpcode(UnknownOpcode opcode)
        {
        }

        protected virtual void VisitDoorAotSe(DoorAotSeOpcode door)
        {
        }

        protected virtual void VisitSceEmSet(SceEmSetOpcode enemy)
        {
        }

        protected virtual void VisitAotReset(AotResetOpcode reset)
        {
        }

        protected virtual void VisitItemAotSet(ItemAotSetOpcode item)
        {
        }

        protected virtual void VisitXaOn(XaOnOpcode sound)
        {
        }

        protected virtual void VisitSceItemGet(SceItemGetOpcode itemGet)
        {
        }
    }

    internal enum BioScriptKind
    {
        Init,
        Main
    }
}
