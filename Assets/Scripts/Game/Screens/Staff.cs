using Casino.Core.Player;
using Casino.UI;
using UnityEngine;

namespace Casino.Screens
{
    /// <summary>Who works where. The same person always staffs the same post, styled by the player's choice.</summary>
    public static class Staff
    {
        static int Slot(StaffRole role, int theme)
        {
            switch (role)
            {
                case StaffRole.Host: return 0;
                case StaffRole.Cashier: return 1;
                case StaffRole.Server: return 2;
                default: return 3 + theme;
            }
        }

        public static int Seed(StaffRole role, int theme)
        {
            switch (role)
            {
                case StaffRole.Host: return 11;
                case StaffRole.Cashier: return 23;
                case StaffRole.Server: return 37;
                default: return 41 + theme * 7;
            }
        }

        public static bool IsFemale(NpcStyle style, StaffRole role, int theme = 0) => NpcStyles.IsFemale(style, Slot(role, theme));

        /// <summary>The name of whoever staffs this post (the bar's second server, theme 1, is the floor waitress).</summary>
        public static string Name(NpcStyle style, StaffRole role, int theme = 0)
        {
            int slot = role == StaffRole.Server && theme > 0 ? 7 + theme : Slot(role, theme);
            return StaffPortrait.NameFor(IsFemale(style, role, theme), slot);
        }

        public static StaffPortrait Portrait(Transform parent, float size, StaffRole role, int theme = 0, bool nameplate = true, NpcStyle? style = null)
        {
            var s = style ?? App.I.Staff;
            return StaffPortrait.Create(parent, size, IsFemale(s, role, theme), Seed(role, theme), role, nameplate, Slot(role, theme));
        }
    }
}
