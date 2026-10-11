namespace Casino.Core.Player
{
    public enum PlayerGender { Male, Female, Unspecified }

    /// <summary>Which NPC look the casino uses for the lobby host, dealers, cashier and bar staff.</summary>
    public enum NpcStyle { Female, Male, Mixed }

    public static class NpcStyles
    {
        /// <summary>Male players get female staff and vice versa; players who don't say get a mix. Changeable in Settings.</summary>
        public static NpcStyle DefaultFor(PlayerGender gender)
        {
            switch (gender)
            {
                case PlayerGender.Male: return NpcStyle.Female;
                case PlayerGender.Female: return NpcStyle.Male;
                default: return NpcStyle.Mixed;
            }
        }

        /// <summary>Whether the NPC in a given slot is female under this style. Mixed alternates by slot.</summary>
        public static bool IsFemale(NpcStyle style, int slot) =>
            style == NpcStyle.Female || (style == NpcStyle.Mixed && (slot & 1) == 0);
    }
}
