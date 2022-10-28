using System;

namespace IntelOrca.Biohazard
{
    internal class Items
    {
        public static string GetItemName(int id)
        {
            if (Enum.IsDefined(typeof(ItemType), (ItemType)id))
            {
                return ((ItemType)id).ToString();
            }
            return $"{id}";
        }
    }
}
