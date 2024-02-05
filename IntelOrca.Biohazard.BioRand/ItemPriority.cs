namespace IntelOrca.Biohazard.BioRand
{
    public enum ItemPriority
    {
        Normal,

        /// <summary>
        /// Do not spawn a key item here.
        /// Useful for items that don't "always" spawn in.
        /// </summary>
        Low,

        /// <summary>
        /// The item must always be the same as the original. Changing or moving
        /// causes issues.
        /// </summary>
        Fixed,

        /// <summary>
        /// The item is hidden and will only be a key item if the preference is set.
        /// </summary>
        Hidden,
    }
}
