using TheOtherRoles.MetaContext;
using TheOtherRoles.Modules;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Roles
{
    public class Cleaner : RoleBase<Cleaner> {
        public static Color color = Palette.ImpostorRed;

        public Cleaner()
        {
            RoleId = roleId = RoleId.Cleaner;
            acTokenChallenge = null;
        }

        public static float cooldown = 30f;

        public AchievementToken<int> acTokenChallenge;

        public override void PostInit()
        {
            if (PlayerControl.LocalPlayer != player) return;
            acTokenChallenge ??= new("cleaner.challenge", 0, (val, _) => val >= 3);
        }

        public static readonly Image Illustration = new TORSpriteLoader("Assets/Sprites/Cleaner.png");

        public override void OnKill(PlayerControl target)
        {
            if (PlayerControl.LocalPlayer == player && HudManagerStartPatch.cleanerCleanButton != null)
                HudManagerStartPatch.cleanerCleanButton.Timer = HudManagerStartPatch.cleanerCleanButton.MaxTimer;
        }

        private static Sprite buttonSprite;
        public static Sprite getButtonSprite() {
            if (buttonSprite) return buttonSprite;
            buttonSprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.CleanButton.png", 115f);
            return buttonSprite;
        }

        public static void clearAndReload() {
            cooldown = CustomOptionHolder.cleanerCooldown.getFloat();
            players = [];
        }
    }
}
