namespace IntelOrca.Biohazard.Script
{
    internal class Bio2ConstantTable : IConstantTable
    {
        public string GetEnemyName(byte kind)
        {
            return $"ENEMY_{((EnemyType)kind).ToString().ToUpperInvariant()}";
        }

        public string GetItemName(byte kind)
        {
            return $"ITEM_{((ItemType)kind).ToString().ToUpperInvariant()}";
        }
    }
}
