using System.Collections.Generic;
using System.Linq;
using Hazel;
using TheOtherRoles.Modules;
using TheOtherRoles.Utilities;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Roles
{
    public class Bait : RoleBase<Bait>
    {
        public Bait()
        {
            RoleId = roleId = RoleId.Bait;
            reportDelay = CustomOptionHolder.baitReportDelay.getFloat();
            reported = true;
            acTokenChallenge = null;
        }

        public override void OnMeetingStart()
        {
            reported = true;
        }

        public override void FixedUpdate()
        {
            if (player != PlayerControl.LocalPlayer) return;

            // Bait report
            if (PlayerControl.LocalPlayer.Data.IsDead && !reported)
            {
                reportDelay -= Time.fixedDeltaTime;
                DeadPlayer deadPlayer = GameHistory.deadPlayers?.Where(x => x.player?.PlayerId == player.PlayerId)?.FirstOrDefault();
                if (deadPlayer.killerIfExisting != null && reportDelay <= 0f)
                {
                    _ = new StaticAchievementToken("bait.common1");

                    Helpers.handleVampireBiteOnBodyReport(); // Manually call Vampire handling, since the CmdReportDeadBody Prefix won't be called
                    Helpers.HandleUndertakerDropOnBodyReport();
                    Helpers.handleTrapperTrapOnBodyReport();

                    byte reporter = deadPlayer.killerIfExisting.PlayerId;

                    if (Madmate.madmate.Any(x => x.PlayerId == player.PlayerId))
                    {
                        var candidates = PlayerControl.AllPlayerControls.GetFastEnumerator().ToArray().Where(x => !x.Data.IsDead && !x.Data.Role.IsImpostor).ToList();
                        int i = rnd.Next(0, candidates.Count);
                        reporter = candidates.Count > 0 ? candidates[i].PlayerId : deadPlayer.killerIfExisting.PlayerId;
                    }

                    acTokenChallenge.Value.killerId = reporter;

                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.UncheckedCmdReportDeadBody, SendOption.Reliable, -1);
                    writer.Write(reporter);
                    writer.Write(player.PlayerId);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    RPCProcedure.uncheckedCmdReportDeadBody(reporter, player.PlayerId);
                    reported = true;
                }
            }

            // Bait Vents
            if (MapUtilities.CachedShipStatus?.AllVents != null)
            {
                var ventsWithPlayers = new List<int>();
                foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                {
                    if (player == null) continue;

                    if (player.inVent)
                    {
                        Vent target = MapUtilities.CachedShipStatus.AllVents.OrderBy(x => Vector2.Distance(x.transform.position, player.GetTruePosition())).FirstOrDefault();
                        if (target != null) ventsWithPlayers.Add(target.Id);
                    }
                }

                foreach (Vent vent in MapUtilities.CachedShipStatus.AllVents)
                {
                    if (vent.myRend == null || vent.myRend.material == null) continue;
                    if (ventsWithPlayers.Contains(vent.Id) || ventsWithPlayers.Count > 0 && highlightAllVents)
                    {
                        vent.myRend.material.SetFloat("_Outline", 1f);
                        vent.myRend.material.SetColor("_OutlineColor", Color.yellow);
                    }
                    else
                        vent.myRend.material.SetFloat("_Outline", 0);
                }
            }
        }

        public override void OnMeetingEnd(PlayerControl exiled = null)
        {
            if (PlayerControl.LocalPlayer == player && PlayerControl.LocalPlayer.Data.IsDead && exiled != null)                 acTokenChallenge.Value.cleared |= acTokenChallenge.Value.killerId == exiled.PlayerId;
        }

        public static Color color = new Color32(0, 247, 255, byte.MaxValue);

        public static bool highlightAllVents = false;
        public float reportDelay = 0f;
        public static bool showKillFlash = true;
        public static bool canBeGuessed = true;

        public bool reported = true;

        public AchievementToken<(byte killerId, bool cleared)> acTokenChallenge = null;

        public override void PostInit()
        {
            if (PlayerControl.LocalPlayer != player) return;
            acTokenChallenge ??= new("bait.challenge", (byte.MaxValue, false), (val, _) => val.cleared);
        }

        public override void OnDeath(PlayerControl killer = null)
        {
            if (killer != null)
            {
                reported = false;
                if (showKillFlash && killer != player && killer == PlayerControl.LocalPlayer)
                    Helpers.showFlash(new Color(204f / 255f, 102f / 255f, 0f / 255f));
            }
        }

        public static void clearAndReload()
        {
            highlightAllVents = CustomOptionHolder.baitHighlightAllVents.getBool();
            showKillFlash = CustomOptionHolder.baitShowKillFlash.getBool();
            canBeGuessed = CustomOptionHolder.baitCanBeGuessed.getBool();
            players = [];
        }
    }
}
