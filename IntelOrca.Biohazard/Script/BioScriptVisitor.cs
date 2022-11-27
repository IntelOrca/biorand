using System;
using System.IO;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    internal class BioScriptVisitor
    {
        protected BioVersion Version;

        public virtual void VisitVersion(BioVersion version)
        {
            Version = version;
        }

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

        public virtual void VisitOpcode(int offset, Span<byte> opcodeBytes)
        {
            VisitOpcode(offset, opcodeBytes.Length, new BinaryReader(new MemoryStream(opcodeBytes.ToArray())));
        }

        private void VisitOpcode(int offset, int length, BinaryReader br)
        {
            var opcode = br.PeekByte();
            if (Version == BioVersion.Biohazard1)
            {
                switch ((OpcodeV1)opcode)
                {
                    default:
                        VisitUnknownOpcode(UnknownOpcode.Read(br, offset, length));
                        break;
                    case OpcodeV1.ElseCk:
                        VisitElseCk(ElseCkOpcode.Read(br, offset));
                        break;
                    case OpcodeV1.DoorAotSe:
                        VisitDoorAotSe(DoorAotSeOpcode.Read(br, offset));
                        break;
                    case OpcodeV1.ItemAotSet:
                        VisitItemAotSet(ItemAotSetOpcode.Read(br, offset));
                        break;
                    case OpcodeV1.SceEmSet:
                        VisitSceEmSet(SceEmSetOpcode.Read(br, offset));
                        break;
                }
                return;
            }
            else
            {
                switch ((OpcodeV2)opcode)
                {
                    default:
                        VisitUnknownOpcode(UnknownOpcode.Read(br, offset, length));
                        break;
                    case OpcodeV2.ElseCk:
                        VisitElseCk(ElseCkOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.Gosub:
                        VisitGosub(GosubOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.Ck:
                        VisitCk(CkOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.Cmp:
                        VisitCmp(CmpOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.AotSet:
                        VisitAotSet(AotSetOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.DoorAotSe:
                        VisitDoorAotSe(DoorAotSeOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.SceEmSet:
                        VisitSceEmSet(SceEmSetOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.AotReset:
                        VisitAotReset(AotResetOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.ItemAotSet:
                        VisitItemAotSet(ItemAotSetOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.XaOn:
                        VisitXaOn(XaOnOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.SceItemGet:
                        VisitSceItemGet(SceItemGetOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.DoorAotSet4p:
                        VisitDoorAotSet4p(DoorAotSet4pOpcode.Read(br, offset));
                        break;
                    case OpcodeV2.ItemAotSet4p:
                        VisitItemAotSet4p(ItemAotSet4pOpcode.Read(br, offset));
                        break;
                }
            }
        }

        protected virtual void VisitOpcode(OpcodeBase opcode)
        {
        }

        protected virtual void VisitUnknownOpcode(UnknownOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitElseCk(ElseCkOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitGosub(GosubOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitCk(CkOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitCmp(CmpOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitAotSet(AotSetOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitDoorAotSe(DoorAotSeOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitSceEmSet(SceEmSetOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitAotReset(AotResetOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitItemAotSet(ItemAotSetOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitXaOn(XaOnOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitSceItemGet(SceItemGetOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitDoorAotSet4p(DoorAotSet4pOpcode opcode) => VisitOpcode(opcode);
        protected virtual void VisitItemAotSet4p(ItemAotSet4pOpcode opcode) => VisitOpcode(opcode);
    }

    internal enum BioScriptKind
    {
        Init,
        Main
    }
}
