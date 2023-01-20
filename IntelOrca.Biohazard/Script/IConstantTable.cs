using System.IO;

namespace IntelOrca.Biohazard.Script
{
    internal interface IConstantTable
    {
        string GetEnemyName(byte kind);
        string GetItemName(byte kind);
        string GetOpcodeSignature(byte opcode);
        string? GetConstant(char kind, int value);
        string? GetConstant(byte opcode, int pIndex, BinaryReader br);
        int? GetConstantValue(string symbol);
        int GetInstructionSize(byte opcode, BinaryReader? br);
        bool IsOpcodeCondition(byte opcode);
        byte? FindOpcode(string name);
    }
}
