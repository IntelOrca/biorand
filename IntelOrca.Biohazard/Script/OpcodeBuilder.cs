using System;
using System.Collections.Generic;
using IntelOrca.Biohazard.Script.Opcodes;

namespace IntelOrca.Biohazard.Script
{
    internal class OpcodeBuilder : BioScriptVisitor
    {
        private readonly List<OpcodeBase> _opcodes = new List<OpcodeBase>();

        public OpcodeBase[] ToArray() => _opcodes.ToArray();

        protected override void VisitOpcode(OpcodeBase opcode) => _opcodes.Add(opcode);
    }
}
