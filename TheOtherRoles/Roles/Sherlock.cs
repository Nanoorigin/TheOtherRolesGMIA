using System;
using System.Collections.Generic;
using System.Linq;
using TheOtherRoles.MetaContext;
using TheOtherRoles.Modules;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Roles
{
    public class Sherlock : RoleBase<Sherlock>
    {
        public static Color color = new Color32(248, 205, 70, byte.MaxValue);

        public static int numTasks = 2;
        public static float cooldown = 10f;
        public static float investigateDistance = 5f;

        public int numUsed;
        public static int killTimerCounter;

        public static List<Tuple<byte, Tuple<byte, Vector3>>> killLog = [];

        private static Sprite watchIcon;
        private static Sprite investigateIcon;
        private static Sprite detectIcon;

        public AchievementToken<bool> acTokenChallenge = null;

        public static HideAndSeekDeathPopup killPopup = null;

        public Sherlock()
        {
            RoleId = roleId = RoleId.Sherlock;
            numUsed = 0;
            acTokenChallenge = null;
        }

        public override void PostInit()
        {
            if (PlayerControl.LocalPlayer != player) return;
            acTokenChallenge ??= new("sherlock.challenge", false, (val, _) => val);
        }

        public static Sprite getInvestigateIcon()
        {
            if (investigateIcon) return investigateIcon;
            investigateIcon = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.SherlockInvestigate.png", 115f);
            return investigateIcon;
        }
        public static Sprite getWatchIcon()
        {
            if (watchIcon) return watchIcon;
            watchIcon = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.SherlockWatch.png", 115f);
            return watchIcon;
        }

        public static Sprite getDetectIcon()
        {
            if (detectIcon) return detectIcon;
            detectIcon = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.SherlockDetectIcon.png", 160f);
            return detectIcon;
        }

        private static TMPro.TMP_Text text;

        public static void investigateMessage(string message, float duration, Color color)
        {

            RoomTracker roomTracker = HudManager.Instance?.roomTracker;

            if (roomTracker != null)
            {
                GameObject gameObject = UnityEngine.Object.Instantiate(roomTracker.gameObject);

                gameObject.transform.SetParent(HudManager.Instance.transform);
                UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<RoomTracker>());

                // Use local position to place it in the player's view instead of the world location
                gameObject.transform.localPosition = new Vector3(0, -1.8f, gameObject.transform.localPosition.z);
                gameObject.transform.localScale *= 1.5f;

                text = gameObject.GetComponent<TMPro.TMP_Text>();
                text.text = message;
                text.color = color;

                HudManager.Instance.StartCoroutine(Effects.Lerp(duration, new Action<float>((p) =>
                {
                    if (p == 1f && text != null && text.gameObject != null)
                        UnityEngine.Object.Destroy(text.gameObject);
                })));
            }
        }

        public int getNumInvestigate()
        {
            int counter = player.Data.Tasks.ToArray().Where(t => t.Complete).Count();
            return (int)Math.Floor((float)counter / numTasks);
        }

        public static void clearAndReload()
        {
            killLog = [];
            numTasks = Mathf.RoundToInt(CustomOptionHolder.sherlockRechargeTasksNumber.getFloat());
            cooldown = CustomOptionHolder.sherlockCooldown.getFloat();
            investigateDistance = CustomOptionHolder.sherlockInvestigateDistance.getFloat();
            killPopup = null;
            players = [];
            if (SherlockDetectArrow.allArrows.Count > 0)                 foreach (var arrow in SherlockDetectArrow.allArrows) {
                    arrow.ArrowObject.SetActive(false);
                    UnityEngine.Object.Destroy(arrow.ArrowObject);
                }
            SherlockDetectArrow.allArrows = [];
            killTimerCounter = 0;
        }

        public class SherlockDetectArrow
        {
            public SpriteRenderer arrowRenderer;
            private SpriteRenderer smallRenderer = null;
            public Vector2 TargetPos;
            public static List<SherlockDetectArrow> allArrows = [];

            public GameObject ArrowObject => arrowRenderer!.gameObject;

            public bool IsSmallenNearPlayer { get; set; } = true;
            public bool IsActive { get; set; } = true;
            public bool IsDisappearing { get; set; } = false;
            public bool WithSmallArrow => smallRenderer != null;
            public bool OnJustPoint { get; set; } = false;
            private float disappearProgress = 0f;
            private static SpriteLoader arrowSprite = SpriteLoader.FromResource("TheOtherRoles.Resources.Arrow.png", 185f);
            private static SpriteLoader arrowSmallSprite = SpriteLoader.FromResource("TheOtherRoles.Resources.ArrowSmall.png", 360f);

            public SherlockDetectArrow(Sprite sprite = null, bool withSmallArrow = false)
            {
                arrowRenderer = Helpers.CreateObject<SpriteRenderer>("Arrow", HudManager.Instance.transform, new Vector3(0, 0, -10f), 5);
                arrowRenderer.sprite = sprite ?? arrowSprite.GetSprite();
                arrowRenderer.sharedMaterial = HatManager.Instance.DefaultShader;
                if (withSmallArrow)
                {
                    smallRenderer = Helpers.CreateObject<SpriteRenderer>("Arrow", arrowRenderer.transform, new Vector3(0, 0, -0.1f), 5);
                    smallRenderer.sprite = arrowSmallSprite.GetSprite();
                }
                allArrows.Add(this);
            }

            private static float perc = 0.925f;
            public void HudUpdate()
            {
                if (!arrowRenderer) return;

                //視点中心からのベクトル

                //表示するカメラ
                Camera main = Helpers.FindCamera(LayerMask.NameToLayer("UI"))!;

                //距離を測るための表示用のカメラ
                Camera worldCam = Camera.main;

                Vector2 del = TargetPos - (Vector2)main.transform.position;

                //目的地との見た目上の離れ具合
                float num = del.magnitude / (worldCam.orthographicSize * perc);

                //近くの矢印を隠す
                bool flag = IsActive && (!IsSmallenNearPlayer || (double)num > 0.3);
                arrowRenderer!.gameObject.SetActive(flag);
                if (!flag) return;

                static bool Between(float value, float min, float max) => value > min && value < max;

                //スクリーン上の位置
                Vector2 viewportPoint = worldCam.WorldToViewportPoint(TargetPos);

                if (Between(viewportPoint.x, 0f, 1f) && Between(viewportPoint.y, 0f, 1f))
                {
                    //画面内を指す矢印
                    arrowRenderer.transform.localPosition = (del - (OnJustPoint ? Vector2.zero : del.normalized * (WithSmallArrow ? 0.9f : 0.6f) * (worldCam.orthographicSize / Camera.main.orthographicSize))).AsVector3(2f);
                    arrowRenderer.transform.localScale = IsSmallenNearPlayer ? Vector3.one * Mathf.Clamp(num, 0f, 1f) : Vector3.one;
                }
                else
                {
                    //画面外を指す矢印
                    Vector2 vector3 = new(Mathf.Clamp(viewportPoint.x * 2f - 1f, -1f, 1f), Mathf.Clamp(viewportPoint.y * 2f - 1f, -1f, 1f));

                    //UIのカメラに合わせて位置を調節する
                    float orthographicSize = main.orthographicSize;
                    float num3 = main.orthographicSize * main.aspect;
                    Vector3 vector4 = new(Mathf.LerpUnclamped(0f, num3 * (WithSmallArrow ? 0.82f : 0.88f), vector3.x), Mathf.LerpUnclamped(0f, orthographicSize * (WithSmallArrow ? 0.72f : 0.79f), vector3.y), 2f);
                    arrowRenderer.transform.localPosition = vector4;
                    arrowRenderer.transform.localScale = Vector3.one;
                }

                arrowRenderer.transform.localScale *= worldCam.orthographicSize / Camera.main.orthographicSize;

                //角度の計算のために正規化する(しなくてもいいのかも)
                del.Normalize();

                arrowRenderer.transform.eulerAngles = new Vector3(0f, 0f, 0f);

                if (smallRenderer != null)
                {
                    var angle = Mathf.Atan2(del.y, del.x) * 180f / Mathf.PI;
                    smallRenderer.transform.localPosition = Vector3.right.RotateZ(angle) * 0.45f;
                    smallRenderer.transform.eulerAngles = new Vector3(0f, 0f, angle);
                }

                if (IsDisappearing || MeetingHud.Instance || !isRole(PlayerControl.LocalPlayer))
                {
                    float a = 1f - disappearProgress;

                    disappearProgress += Time.deltaTime * 0.85f;
                    arrowRenderer.color = new Color(arrowRenderer.color.r, arrowRenderer.color.g, arrowRenderer.color.b, a);
                    if (smallRenderer != null) smallRenderer.color = new(1f, 1f, 1f, a);

                    if (disappearProgress > 1f)
                    {
                        if (arrowRenderer) UnityEngine.Object.Destroy(arrowRenderer!.gameObject);
                        arrowRenderer = null;
                        disappearProgress = 1f;
                        allArrows.Remove(this);
                    }
                }
            }

            public void MarkAsDisappering()
            {
                IsDisappearing = true;
            }
        }
    }
}
