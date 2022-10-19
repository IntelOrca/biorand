namespace rer
{
    public enum ItemPriority
    {
        Normal,

        /// <summary>
        /// Do not spawn a key item here.
        /// Useful for items that don't "always" spawn in.
        /// </summary>
        Low
    }
}
