namespace IntelOrca.Biohazard.Script
{
    internal interface IConstantTable
    {
        string GetEnemyName(byte kind);
        string GetItemName(byte kind);
    }
}
