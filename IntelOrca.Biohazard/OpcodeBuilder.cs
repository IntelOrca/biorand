using System;
using System.Collections.Generic;
using IntelOrca.Biohazard.Opcodes;

namespace IntelOrca.Biohazard
{
    internal class OpcodeBuilder : BioScriptVisitor
    {
        private readonly List<OpcodeBase> _opcodes = new List<OpcodeBase>();

        public OpcodeBase[] ToArray() => _opcodes.ToArray();

        protected override void VisitUnknownOpcode(UnknownOpcode opcode) => _opcodes.Add(opcode);
        protected override void VisitDoorAotSe(DoorAotSeOpcode opcode) => _opcodes.Add(opcode);
        protected override void VisitItemAotSet(ItemAotSetOpcode opcode) => _opcodes.Add(opcode);
        protected override void VisitSceEmSet(SceEmSetOpcode opcode) => _opcodes.Add(opcode);
        protected override void VisitAotReset(AotResetOpcode opcode) => _opcodes.Add(opcode);
        protected override void VisitXaOn(XaOnOpcode opcode) => _opcodes.Add(opcode);
        protected override void VisitSceItemGet(SceItemGetOpcode opcode) => _opcodes.Add(opcode);
    }
}
