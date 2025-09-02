using System;
using System.Collections.Generic;
using System.Linq;
using Hazel;
using TheOtherRoles.MetaContext;
using TheOtherRoles.Modules;
using TheOtherRoles.Utilities;
using UnityEngine;
using static TheOtherRoles.Patches.PlayerControlFixedUpdatePatch;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Roles
{
    public class Akujo : RoleBase<Akujo>
    {
        public static Color color = new Color32(142, 69, 147, byte.MaxValue);
        public PlayerControl honmei;
        public List<PlayerControl> keeps = [];
        public PlayerControl currentTarget;
        public PlayerControl cupidHonmei;
        public DateTime startTime;

        public Akujo()
        {
            RoleId = roleId = RoleId.Akujo;
            honmei = null;
            keeps = [];
            currentTarget = null;
            cupidHonmei = null;
            startTime = DateTime.UtcNow;
            iconColor = getAvailableColor();
        }

        public void akujoUpdate()
        {
            if (PlayerControl.LocalPlayer != player) return;
            if (HudManagerStartPatch.akujoTimeRemainingText != null)
                HudManagerStartPatch.akujoTimeRemainingText.enabled = false;
            if (!player.Data.IsDead && cupidHonmei == null && honmei == null)
            {
                if (HudManagerStartPatch.akujoTimeRemainingText != null)
                {
                    HudManagerStartPatch.akujoTimeRemainingText.text = TimeSpan.FromSeconds(timeLeft).ToString(@"mm\:ss");
                    HudManagerStartPatch.akujoTimeRemainingText.enabled = Helpers.ShowButtons;
                }
                if (timeLeft <= 0)
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AkujoSuicide, SendOption.Reliable, -1);
                    writer.Write(player.PlayerId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.akujoSuicide(player.PlayerId);
                }
            }
        }

        public void akujoSetTarget()
        {
            if (PlayerControl.LocalPlayer != player || PlayerControl.LocalPlayer.Data.IsDead) return;
            var untargetables = new List<PlayerControl>();
            if (honmei != null) untargetables.Add(honmei);
            if (keeps != null) untargetables.AddRange(keeps);
            if (Cupid.isCupidLovers(player))
            {
                var cupid = Cupid.players.FirstOrDefault(x => x.lovers1 == player || x.lovers2 == player);
                if (cupid != null)
                {
                    untargetables.Add(cupid.lovers1);
                    untargetables.Add(cupid.lovers2);
                }
            }
            currentTarget = setTarget(untargetablePlayers: untargetables);
            if (honmei == null && cupidHonmei == null || keepsLeft > 0) setPlayerOutline(currentTarget, color);
        }

        public override void FixedUpdate()
        {
            akujoSetTarget();
            akujoUpdate();
        }

        public static List<Color> iconColors =
            [
                color,                         // pink
                new Color32(255, 165, 0, 255), // orange
                new Color32(255, 255, 0, 255), // yellow
                new Color32(0, 255, 0, 255),   // green
                new Color32(0, 0, 255, 255),   // blue
                new Color32(0, 255, 255, 255), // light blue
                new Color32(255, 0, 0, 255),   // red
            ];

        public int timeLeft { get { return (int)Math.Ceiling(timeLimit - (DateTime.UtcNow - local.startTime).TotalSeconds); } }

        public int keepsLeft { get { return numKeeps - keeps.Count; } }

        public static int numAlive
        {
            get
            {
                int alive = 0;
                foreach (var p in players)
                    if (p.player && !p.player.Data.IsDead && p.honmei != null && !p.honmei.Data.IsDead)
                        alive++;
                return alive;
            }
        }

        public static bool isPartner(PlayerControl player, PlayerControl partner)
        {
            var akujo = getRole(player);
            if (akujo != null)
                return akujo.isPartner(partner);
            return false;
        }

        public bool isPartner(PlayerControl partner)
        {
            return honmei == partner || keeps.Contains(partner);
        }

        public static bool isHonmei(PlayerControl player)
        {
            return players.Any(x => x.player && x.honmei == player);
        }

        public static bool isKeep(PlayerControl player)
        {
            return players.Any(x => x.player && x.keeps.Contains(player));
        }

        public static Color getAvailableColor()
        {
            var availableColors = new List<Color>(iconColors);
            foreach (var akujo in players)
                availableColors.RemoveAll(x => x == akujo.iconColor);
            return availableColors.Count > 0 ? availableColors[0] : color;
        }

        public Color iconColor;

        public static float timeLimit = 1300f;
        public static bool knowsRoles = true;
        public static int numKeeps;

        public static readonly HelpSprite[] HelpSprites = [
            new(getHonmeiSprite(), "akujoHonmeiHint"),
            new(getKeepSprite(), "akujoKeepHint")
        ];

        public static readonly Image Illustration = new TORSpriteLoader("Assets/Akujo.png");

        private static Sprite honmeiSprite;
        public static Sprite getHonmeiSprite()
        {
            if (honmeiSprite) return honmeiSprite;
            honmeiSprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.AkujoHonmeiButton.png", 115f);
            return honmeiSprite;
        }

        private static Sprite keepSprite;
        public static Sprite getKeepSprite()
        {
            if (keepSprite) return keepSprite;
            keepSprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.AkujoKeepButton.png", 115f);
            return keepSprite;
        }

        public override void OnDeath(PlayerControl killer = null)
        {
            if (honmei && !honmei.Data.IsDead)
            {
                if (killer != null)
                    honmei.MurderPlayer(honmei, MurderResultFlags.Succeeded);
                else
                {
                    if (honmei.isRole(RoleId.NekoKabocha))
                        NekoKabocha.getRole(honmei).otherKiller = player;
                    honmei.Exiled();
                    if (PlayerControl.LocalPlayer == honmei && Helpers.ShowKillAnimation)
                        FastDestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(honmei.Data, honmei.Data);
                }

                if (Busker.players.Any(x => x.player == honmei && x.pseudocideFlag) && PlayerControl.LocalPlayer == honmei)
                {
                    var busker = Busker.getRole(honmei);
                    busker.dieBusker(true);
                }
                GameHistory.overrideDeathReasonAndKiller(honmei, DeadPlayer.CustomDeathReason.LoverSuicide);
            }
        }

        public static void killAkujo(PlayerControl player, PlayerControl killer = null)
        {
            if (player == null || !isHonmei(player)) return;
            var akujo = players.FirstOrDefault(x => x.honmei == player);
            if (akujo != null && akujo.player && !akujo.player.Data.IsDead)
            {
                if (killer != null)
                    akujo.player.MurderPlayer(akujo.player, MurderResultFlags.Succeeded);
                else
                {
                    akujo.player.Exiled();
                    if (PlayerControl.LocalPlayer == akujo.player && Helpers.ShowKillAnimation)
                        FastDestroyableSingleton<HudManager>.Instance.KillOverlay.ShowKillAnimation(akujo.player.Data, akujo.player.Data);
                }
                GameHistory.overrideDeathReasonAndKiller(akujo.player, DeadPlayer.CustomDeathReason.LoverSuicide);
            }
        }

        public override void ResetRole(bool isShifted)
        {
            if (!isShifted)
            {
                honmei = null;
                keeps = [];
            }
            if (PlayerControl.LocalPlayer == player)
            {
                removeInfoText();
                if (HudManagerStartPatch.akujoTimeRemainingText != null)
                    HudManagerStartPatch.akujoTimeRemainingText.enabled = false;
            }
        }

        public void removeInfoText()
        {
            if (player == null) return;
            if (honmei != null)
            {
                Transform playerInfoTransform = honmei.cosmetics.nameText.transform.parent.FindChild("Info");
                TMPro.TextMeshPro playerInfo = playerInfoTransform?.GetComponent<TMPro.TextMeshPro>();
                if (playerInfo != null) playerInfo.text = "";
            }
            if (keeps != null)
            {
                foreach (PlayerControl playerControl in keeps)
                {
                    Transform playerInfoTransform = playerControl.cosmetics.nameText.transform.parent.FindChild("Info");
                    TMPro.TextMeshPro playerInfo = playerInfoTransform?.GetComponent<TMPro.TextMeshPro>();
                    if (playerInfo != null) playerInfo.text = "";
                }
            }
        }

        public static void breakLovers(PlayerControl lover)
        {
            if (lover.isLovers())
            {
                PlayerControl otherLover = lover.getPartner();
                if (otherLover != null)
                {
                    Lovers.eraseCouple(lover);
                    otherLover.MurderPlayer(otherLover, MurderResultFlags.Succeeded);
                    GameHistory.overrideDeathReasonAndKiller(otherLover, DeadPlayer.CustomDeathReason.LoveStolen);
                }
            }
            if (isKeep(lover) || isHonmei(lover))
            {
                var akujo = players.FirstOrDefault(x => x.keeps.Contains(lover) || x.honmei == lover);
                akujo.removeInfoText();
                akujo.keeps.Remove(lover);
                akujo.honmei = null;
            }
        }

        public override void HandleDisconnect(PlayerControl player, DisconnectReasons reason)
        {
            if (this.player == player)
            {
                honmei = null;
                keeps = [];
            }
        }

        public static void clearAndReload()
        {
            timeLimit = CustomOptionHolder.akujoTimeLimit.getFloat() + 10f;
            knowsRoles = CustomOptionHolder.akujoKnowsRoles.getBool();
            numKeeps = Math.Min((int)CustomOptionHolder.akujoNumKeeps.getFloat(), PlayerControl.AllPlayerControls.Count - 2);
            players = [];
        }
    }
}
