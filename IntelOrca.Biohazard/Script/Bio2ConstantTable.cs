namespace IntelOrca.Biohazard.Script
{
    internal class Bio2ConstantTable : IConstantTable
    {
        public byte? FindOpcode(string name)
        {
            throw new System.NotImplementedException();
        }

        public string? GetConstant(char kind, int value)
        {
            throw new System.NotImplementedException();
        }

        public int? GetConstantValue(string symbol)
        {
            throw new System.NotImplementedException();
        }

        public string GetEnemyName(byte kind)
        {
            return $"ENEMY_{((EnemyType)kind).ToString().ToUpperInvariant()}";
        }

        public int GetInstructionSize(byte opcode)
        {
            throw new System.NotImplementedException();
        }

        public string GetItemName(byte kind)
        {
            return $"ITEM_{((ItemType)kind).ToString().ToUpperInvariant()}";
        }

        public string GetOpcodeSignature(byte opcode)
        {
            throw new System.NotImplementedException();
        }
    }
}
