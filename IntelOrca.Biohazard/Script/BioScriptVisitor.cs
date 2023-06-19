using System;
using System.IO;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    public class BioScriptVisitor
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

        public virtual void VisitTrailingData(int offset, Span<byte> data)
        {
        }

        public virtual void VisitOpcode(int offset, Span<byte> opcodeBytes)
        {
            VisitOpcode(offset, opcodeBytes.Length, new BinaryReader(new MemoryStream(opcodeBytes.ToArray())));
        }

        private void VisitOpcode(int offset, int length, BinaryReader br)
        {
            var opcode = ReadOpcode(offset, length, br);
            VisitOpcode(opcode);
        }

        protected virtual void VisitOpcode(OpcodeBase opcode)
        {
        }

        private OpcodeBase ReadOpcode(int offset, int length, BinaryReader br)
        {
            var opcode = br.PeekByte();
            switch (Version)
            {
                case BioVersion.Biohazard1:
                    return (OpcodeV1)opcode switch
                    {
                        OpcodeV1.ElseCk => ElseCkOpcode.Read(br, offset),
                        OpcodeV1.TestPickup => TestPickupOpcode.Read(br, offset),
                        OpcodeV1.DoorAotSe => DoorAotSeOpcode.Read(br, offset),
                        OpcodeV1.ItemAotSet => ItemAotSetOpcode.Read(br, offset),
                        OpcodeV1.SceEmSet => SceEmSetOpcode.Read(br, offset),
                        _ => UnknownOpcode.Read(br, offset, length),
                    };
                case BioVersion.Biohazard2:
                    return (OpcodeV2)opcode switch
                    {
                        OpcodeV2.EvtExec => EvtExecOpcode.Read(br, offset),
                        OpcodeV2.ElseCk => ElseCkOpcode.Read(br, offset),
                        OpcodeV2.Gosub => GosubOpcode.Read(br, offset),
                        OpcodeV2.Ck => CkOpcode.Read(br, offset),
                        OpcodeV2.Cmp => CmpOpcode.Read(br, offset),
                        OpcodeV2.AotSet => AotSetOpcode.Read(br, offset),
                        OpcodeV2.AotSet4p => AotSet4pOpcode.Read(br, offset),
                        OpcodeV2.DoorAotSe => DoorAotSeOpcode.Read(br, offset),
                        OpcodeV2.SceEmSet => SceEmSetOpcode.Read(br, offset),
                        OpcodeV2.AotReset => AotResetOpcode.Read(br, offset),
                        OpcodeV2.ItemAotSet => ItemAotSetOpcode.Read(br, offset),
                        OpcodeV2.XaOn => XaOnOpcode.Read(br, offset),
                        OpcodeV2.SceItemGet => SceItemGetOpcode.Read(br, offset),
                        OpcodeV2.DoorAotSet4p => DoorAotSet4pOpcode.Read(br, offset),
                        OpcodeV2.ItemAotSet4p => ItemAotSet4pOpcode.Read(br, offset),
                        _ => UnknownOpcode.Read(br, offset, length),
                    };
                case BioVersion.Biohazard3:
                    return (OpcodeV3)opcode switch
                    {
                        OpcodeV3.EvtExec => EvtExecOpcode.Read(br, offset),
                        OpcodeV3.ElseCk => ElseCkOpcode.Read(br, offset),
                        OpcodeV3.Gosub => GosubOpcode.Read(br, offset),
                        OpcodeV3.Ck => CkOpcode.Read(br, offset),
                        OpcodeV3.Cmp => CmpOpcode.Read(br, offset),
                        OpcodeV3.AotSet => AotSetOpcode.Read(br, offset),
                        OpcodeV3.AotSet4p => AotSet4pOpcode.Read(br, offset),
                        OpcodeV3.DoorAotSe => DoorAotSeOpcode.Read(br, offset),
                        OpcodeV3.SceEmSet => SceEmSetOpcode.Read(br, offset),
                        OpcodeV3.AotReset => AotResetOpcode.Read(br, offset),
                        OpcodeV3.ItemAotSet => ItemAotSetOpcode.Read(br, offset),
                        OpcodeV3.DoorAotSet4p => DoorAotSet4pOpcode.Read(br, offset),
                        OpcodeV3.ItemAotSet4p => ItemAotSet4pOpcode.Read(br, offset),
                        _ => UnknownOpcode.Read(br, offset, length),
                    };
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public enum BioScriptKind
    {
        Init,
        Main,
        Event
    }
}
