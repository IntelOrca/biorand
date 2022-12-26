namespace IntelOrca.Biohazard.Script
{
    internal interface IConstantTable
    {
        string GetEnemyName(byte kind);
        string GetItemName(byte kind);
        string GetOpcodeSignature(byte opcode);
        string? GetConstant(char kind, int value);
        int? GetConstantValue(string symbol);
        int GetInstructionSize(byte opcode);
        byte? FindOpcode(string name);
    }
}
