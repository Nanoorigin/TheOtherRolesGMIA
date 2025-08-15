using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Rewired.Internal;
using TheOtherRoles.MetaContext;
using TheOtherRoles.Roles;
using TheOtherRoles.Utilities;
using UnityEngine;

namespace TheOtherRoles.Modules;
public class HelpSprite
{
    public SpriteLoader sprite;
    public string localizedName;

    public HelpSprite(Sprite sprite, string localizedName)
    {
        this.sprite = Helpers.ConvertSpriteToSpriteLoader(sprite);
        this.localizedName = localizedName;
    }

    public HelpSprite(SpriteLoader sprite, string localizedName)
    {
        this.sprite = sprite;
        this.localizedName = localizedName;
    }
}

public static class HelpMenu
{
    [Flags]
    public enum HelpTab
    {
        Achievements = 0x01,
        Roles = 0x02,
        Options = 0x04,
        MyInfo = 0x10,
        Overview = 0x20,
    }

    private static TextAttribute TabButtonAttr = new(TextAttribute.BoldAttr) { Size = new(1.15f, 0.26f) };
    private static float HelpHeight = 4.1f;

    private static MetaScreen lastHelpScreen = null;
    public static bool OpenedAnyHelpScreen => lastHelpScreen;
    public static MetaScreen LastHelpScreen => lastHelpScreen;

    public record HelpTabInfo(HelpTab Tab, string TranslateKey)
    {
        public MetaContextOld.Button GetButton(MetaScreen screen, HelpTab currentTab, HelpTab validTabs) => new(() => ShowScreen(screen, Tab, validTabs), TabButtonAttr) { Color = currentTab == Tab ? Color.white : Color.gray, TranslationKey = TranslateKey };
    }

    public static HelpTabInfo[] AllHelpTabInfo = [
        new(HelpTab.MyInfo, "helpMyInfoKey"),
        new(HelpTab.Roles, "helpRolesKey"),
        new(HelpTab.Achievements, "helpAchievementsKey"),
        new(HelpTab.Overview, "helpOverviewKey"),
        new(HelpTab.Options, "helpOptionsKey"),
    ];

    private static IMetaContextOld GetTabsContext(MetaScreen screen, HelpTab tab, HelpTab validTabs)
    {
        List<IMetaParallelPlacableOld> tabs = new();

        foreach (var info in AllHelpTabInfo) if ((validTabs & info.Tab) != 0) tabs.Add(info.GetButton(screen, tab, validTabs));

        return new CombinedContextOld(0.5f, tabs.ToArray());
    }

    private static IMetaContextOld ShowAchievementScreen()
    {
        Artifact<GUIScreen> artifact = null!;
        Predicate<Achievement> achPredicate = null;
        string shownPredicate = null;
        string scrollerTagPrefix = "ingameAchievementViewer";
        string scrollerTagPostfix = "_categorized";

        GUIContext GenerateContext() => AchievementViewer.GenerateContext(3.15f, 6.2f, scrollerTagPrefix + scrollerTagPostfix, achPredicate, shownPredicate);
        void ShowInnerContext() => artifact.Do(screen => screen.SetContext(GenerateContext(), out var _));

        var gui = TORGUIContextEngine.API;

        List<GUIContext> categorizeButtons = [
            gui.LocalizedText(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.StandardMediumMasked), "achievementFilter"),
            new GUIModernButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievementAllFilter")){ OnClick = _ => {
                achPredicate = null;
                scrollerTagPrefix = "ingameAchievementViewer";
                shownPredicate = null;
                ShowInnerContext();
            }, SelectedDefault = true, WithCheckMark = true },
            new GUIModernButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievementAchieved")){ OnClick = _ => {
                achPredicate = a => a.IsCleared;
                scrollerTagPrefix = "ingameAchievementViewerCleared";
                shownPredicate = ModTranslation.getString("achievementAchieved");
                ShowInnerContext();
            }, WithCheckMark = true},
            new GUIModernButton(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.CenteredBoldFixed), new TranslateTextComponent("achievementNonAchieved")){ OnClick = _ => {
                achPredicate = a => !a.IsCleared;
                scrollerTagPrefix = "ingameAchievementViewerNotCleared";
                shownPredicate = ModTranslation.getString("achievementNonAchieved");
                ShowInnerContext();
            }, WithCheckMark = true}];

        var sidebar = new VerticalContextsHolder(GUIAlignment.Center,
            new GUIButtonGroup(new VerticalContextsHolder(GUIAlignment.Top, categorizeButtons))
            );

        var screen = new GUIFixedView(GUIAlignment.Top, new(5.7f, 3.8f), GenerateContext()) { WithMask = false };
        artifact = screen.Artifact;
        return new MetaContextOld.WrappedContext(new HorizontalContextsHolder(GUIAlignment.Center, screen, sidebar));
    }

    static public GUIContext GetImageContent(Image image, GUIContext context) => TORGUIContextEngine.API.HorizontalHolder(GUIAlignment.Left,
        new TORGUIImage(GUIAlignment.Left, image, new(0.55f, null)) { IsMasked = true}, TORGUIContextEngine.API.HorizontalMargin(0.15f), context);
    static public GUIContext GetImageContent(string imagePath, string rawText) => GetImageContent(SpriteLoader.FromResource(imagePath, 100f),
        rawText == null ? TORGUIContextEngine.API.EmptyContext : TORGUIContextEngine.API.RawText(GUIAlignment.Left, TORGUIContextEngine.API.GetAttribute(AttributeAsset.DocumentStandard), rawText));

    private static readonly TextAttribute RoleTitleAttr = new(TextAttribute.BoldAttr) { Size = new Vector2(1.4f, 0.29f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial };

    private static IMetaContextOld ShowAssignableScreen()
    {
        (IEnumerable<RoleInfo> roleInfo, TextComponent label)[] assignables = [
            (RoleInfo.allRoleInfos.Where(x => x.isImpostor && x.roleId != RoleId.BomberB && !(TORMapOptions.gameMode == CustomGamemodes.Guesser && x.roleId == RoleId.EvilGuesser)), TORGUIContextEngine.API.TextComponent(Palette.ImpostorRed, "impostor")),
            (RoleInfo.allRoleInfos.Where(x => !x.isImpostor && !x.isNeutral && !x.isModifier && !(TORMapOptions.gameMode == CustomGamemodes.Guesser && x.roleId == RoleId.NiceGuesser)), TORGUIContextEngine.API.TextComponent(Palette.CrewmateBlue, "crewmate")),
            (RoleInfo.allRoleInfos.Where(x => x.isNeutral && !x.isModifier), TORGUIContextEngine.API.TextComponent(new Color32(76, 84, 78, 255), "roleIntroNeutral")),
            (RoleInfo.allRoleInfos.Where(x => x.isModifier), TORGUIContextEngine.API.TextComponent(Color.yellow, "modifiers"))
            ];

        MetaContextOld inner = new();
        void AddContent(TextComponent label, Action routine)
        {
            if (inner.Count > 0) inner.Append(new MetaContextOld.VerticalMargin(0.2f));
            inner.Append(new MetaContextOld.WrappedContext(new TORGUIText(GUIAlignment.Left, TORGUIContextEngine.API.GetAttribute(AttributeAsset.DocumentTitle), label)));
            inner.Append(new MetaContextOld.VerticalMargin(0.1f));
            routine?.Invoke();
        }

        foreach (var content in assignables)
        {
            AddContent(content.label, (Action)(() =>
            {
                inner.Append(content.roleInfo, (role) => new MetaContextOld.Button(() =>
                {
                    OpenAssignableHelp(role);
                }, RoleTitleAttr)
                {
                    RawText = Helpers.cs(role.color, role.name),
                    PostBuilder = (PassiveButton button, SpriteRenderer renderer, TMPro.TextMeshPro text) =>
                    {
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        button.OnMouseOver.AddListener((Action)(() =>
                        {
                            TORGUIManager.Instance.SetHelpContext(button, GetAssignableOverlay(role));
                        }));
                        button.OnMouseOut.AddListener((Action)(() => TORGUIManager.Instance.HideHelpContextIf(button)));
                    },
                    Alignment = IMetaContextOld.AlignmentOption.Center
                }, 4, -1, 0, 0.6f);
            }));
        }

        return new MetaContextOld.ScrollView(new(7.4f, HelpHeight), inner) { Alignment = IMetaContextOld.AlignmentOption.Center };
    }

    private static Reference<MetaContextOld.ScrollView.InnerScreen> optionsInner = new();
    private static TextAttribute OptionsAttr = new(TextAttribute.BoldAttr) { FontSize = 1.6f, FontMaxSize = 1.6f, FontMinSize = 1.6f, Size = new(4f, 10f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Alignment = TMPro.TextAlignmentOptions.TopLeft };
    private static IMetaContextOld ShowOptionsScreen()
    {
        return new MetaContextOld.ScrollView(new(7.4f, HelpHeight), GetOptionsContext()) { Alignment = IMetaContextOld.AlignmentOption.Center, ScrollerTag = "HelpOptions", InnerRef = optionsInner };
    }

    static private IMetaContextOld GetOptionsContext()
    {
        return new MetaContextOld.VariableText(OptionsAttr) { RawText = GameOptionsDataPatch.buildAllOptions(), Alignment = IMetaContextOld.AlignmentOption.Center };
    }

    internal static void OnUpdateOptions()
    {
        if (!(optionsInner?.Value?.IsValid ?? false)) return;

        optionsInner.Value.SetContext(GetOptionsContext());
    }

    private static void OpenAssignableHelp(RoleInfo roleInfo)
    {
        var screen = MetaScreen.GenerateWindow(new(7f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, true, background: BackgroundSetting.Modern);
        Artifact<GUIScreen> inner = null;
        var scrollView = new GUIScrollView(GUIAlignment.Left, new(7f, 4.5f), () => GetRoleContext(roleInfo) ?? GUIEmptyContext.Default);
        inner = scrollView.Artifact;
        Reference<MetaContextOld.ScrollView.InnerScreen> innerRef = new();

        screen.SetContext(scrollView, out _);
    }

    public static GUIContext GetRoleContext(RoleInfo roleInfo, params GUIContext[] inner)
    {
        var notNullContext = inner.Where(x => x != null);
        return TORGUIContextEngine.API.VerticalHolder(GUIAlignment.Left, [GetRoleNameContext(roleInfo), GetConfigurationsChapter(roleInfo), GetRoleHelpSprite(roleInfo), .. notNullContext, GetAchievementContext(roleInfo)]);
    }

    static private TextAttributes RoleBlurbAttribute = new(TORGUIContextEngine.API.GetAttribute(AttributeAsset.DocumentBold)) { FontSize = new(1.3f) };
    static private TextAttributes RoleNameAttribute = new(TORGUIContextEngine.API.GetAttribute(AttributeAsset.DocumentBold)) { FontSize = new(2.8f) };
    static private TextAttributes ChapterTitleAttribute = new(TORGUIContextEngine.API.GetAttribute(AttributeAsset.DocumentBold)) { FontSize = new(1.7f) };

    private static GUIContext GetRoleNameContext(RoleInfo roleInfo)
    {
        var gui = TORGUIContextEngine.API;
        if (roleInfo == null) return null;
        var blurb = gui.Text(GUIAlignment.Left, RoleBlurbAttribute, gui.RawTextComponent(Helpers.cs(roleInfo.color, roleInfo.introDescription) ?? "ERROR"));
        var title = gui.Text(GUIAlignment.Left, RoleNameAttribute, gui.RawTextComponent(Helpers.cs(roleInfo.color, roleInfo.name) ?? "ERROR"));

        return gui.VerticalHolder(GUIAlignment.TopLeft, gui.HorizontalHolder(GUIAlignment.Left, gui.VerticalHolder(GUIAlignment.Left, blurb, gui.VerticalMargin(-0.06f), title, gui.VerticalMargin(-0.05f)), gui.HorizontalMargin(0.25f)), gui.VerticalMargin(0.15f),
            gui.RawText(GUIAlignment.Left, gui.GetAttribute(AttributeAsset.DocumentStandard), roleInfo.fullDescription), gui.VerticalMargin(0.15f));
    }

    private static GUIContext GetRoleHelpSprite(RoleInfo roleInfo)
    {
        var gui = TORGUIContextEngine.API;
        if (roleInfo == null) return null;

        GUIContext GetHelpSprite(HelpSprite hs) => new HorizontalContextsHolder(GUIAlignment.Left,
            gui.VerticalMargin(0.15f),
            gui.Image(GUIAlignment.Left, hs.sprite, new FuzzySize(0.4f, null)),
            gui.HorizontalMargin(0.15f), gui.LocalizedText(GUIAlignment.Left, gui.GetAttribute(AttributeAsset.DocumentStandard), hs.localizedName)
            );

        var context = roleInfo.helpSprite.Select(GetHelpSprite);
        if (roleInfo.helpSprite.Length == 0) return gui.EmptyContext;
        return GetChapter("ability", [gui.VerticalHolder(GUIAlignment.Left, context)]);
    }

    public static void TryCloseHelpScreen()
    {
        if (lastHelpScreen) lastHelpScreen!.CloseScreen();
    }
    public static void TryOpenHelpScreen(HelpTab tab)
    {
        if (!lastHelpScreen) lastHelpScreen = OpenHelpScreen(tab);
    }

    private static TextAttribute RoleTitleAttrUnmasked = new(TextAttribute.BoldAttr) { Size = new Vector2(1.4f, 0.29f) };

    private static IMetaContextOld ShowMyRolesSrceen()
    {
        MetaContextOld widget = new();

        Artifact<GUIScreen> inner = null!;

        widget.Append(RoleInfo.getRoleInfoForPlayer(PlayerControl.LocalPlayer).OrderBy(x => x.isModifier),
            (role) => new MetaContextOld.Button(() =>
            {
                inner.Do(screen =>
                {
                    screen.SetContext(GetRoleContext(role), out _);
                });
            }, RoleTitleAttrUnmasked)
            {
                RawText = Helpers.cs(role.color, role.name),
                Alignment = IMetaContextOld.AlignmentOption.Center
            }, 128, -1, 0, 0.6f);

        var assignable = RoleInfo.getRoleInfoForPlayer(PlayerControl.LocalPlayer, false).FirstOrDefault();
        var scrollView = new GUIScrollView(GUIAlignment.Left, new(7.4f, HelpHeight - 0.7f), () =>
        {
            return GetRoleContext(assignable) ?? GUIEmptyContext.Default;
        });
        inner = scrollView.Artifact;

        widget.Append(new MetaContextOld.WrappedContext(scrollView));

        return widget;
    }

    static public GUIContext GetChapter(string chapterName, GUIContext[] inner)
    {
        if (inner.Contains(TORGUIContextEngine.API.EmptyContext)) return TORGUIContextEngine.API.EmptyContext;
        return TORGUIContextEngine.API.VerticalHolder(GUIAlignment.TopLeft,
            TORGUIContextEngine.API.LocalizedText(GUIAlignment.TopLeft, ChapterTitleAttribute, chapterName),
            TORGUIContextEngine.API.HorizontalHolder(GUIAlignment.TopLeft, TORGUIContextEngine.API.HorizontalMargin(0.1f), TORGUIContextEngine.API.VerticalHolder(GUIAlignment.Left, inner.Where(w => w != null).Delimit(TORGUIContextEngine.API.VerticalMargin(0.1f)))),
            TORGUIContextEngine.API.VerticalMargin(0.1f)
            );
    }

    static public GUIContext GetConfigurationsChapter(RoleInfo assignable) => GetChapter("configurationsTitle", [GetConfigurationsContext(assignable)]);

    public static string optionToString(CustomOption option)
    {
        if (option == null) return "";
        return $"{option.getName().Replace("- ", "")}: {option.getString()}";
    }

    public static string optionsToString(CustomOption option, bool skipFirst = false)
    {
        if (option == null) return "";

        List<string> options = [];
        if (!skipFirst) options.Add(optionToString(option));
        if (option.getBool())
        {
            foreach (CustomOption op in option.children)
            {
                string str = optionsToString(op);
                if (str != "") options.Add(str);
            }
        }
        return string.Join("\n", options);
    }

    public enum RoleCategory
    {
        Crewmates,
        Impostors,
        Neutrals
    }

    private static IMetaContextOld ShowPreviewScreen()
    {
        var gui = TORGUIContextEngine.API;
        var textAttr = gui.GetAttribute(AttributeAsset.OverlayContent);
        var maskedAttr = gui.GetAttribute(AttributeAsset.DocumentStandard);
        var maskedTitleAttr = gui.GetAttribute(AttributeAsset.DocumentTitle);
        var maskedSubtitleAttr = gui.GetAttribute(AttributeAsset.DocumentSubtitle1);
        GUIContext GetAssignableText(RoleInfo assignable, string displayName = null) => new TORGUIText(GUIAlignment.Center, maskedAttr, new RawTextComponent(displayName ?? Helpers.cs(assignable.color, assignable.name)))
        {
            OverlayContext = () => GetAssignableOverlay(assignable),
            OnClickText = (() => OpenAssignableHelp(assignable), false)
        };

        GUIContext AddAdditional(RoleInfo role, string text) => gui.HorizontalHolder(GUIAlignment.Left, gui.HorizontalMargin(0.1f), GetAssignableText(role, null), gui.HorizontalMargin(0.1f), gui.RawText(GUIAlignment.Left, maskedAttr, text));

        void AddAllAdditional(CustomOption option, List<GUIContext> context)
        {
            if (option == CustomOptionHolder.sheriffSpawnRate && CustomOptionHolder.deputySpawnRate.getSelection() > 0)
                context.Add(AddAdditional(RoleInfo.deputy, $"x{CustomOptionHolder.deputyRoleCount.getFloat()}" +
                    (CustomOptionHolder.deputySpawnRate.getSelection() == 10 ? "" : $" ({CustomOptionHolder.deputySpawnRate.getString()})")));
            else if (option == CustomOptionHolder.jackalSpawnRate)
                context.Add(AddAdditional(RoleInfo.sidekick, CustomOptionHolder.jackalCanCreateSidekick.getString()));
            else if (option == CustomOptionHolder.foxSpawnRate)
                context.Add(AddAdditional(RoleInfo.immoralist, CustomOptionHolder.foxCanCreateImmoralist.getString()));
        }

        GUIContext GetRoleOverview(RoleCategory category, string categoryName)
        {
            List<GUIContext> list100 = [], listRandom = [], ghosts = [], modifiers = [];

            void CheckRoles(List<GUIContext> list100, List<GUIContext> listRandom)
            {
                foreach (var role in RoleInfo.allRoleInfos.Where(x => (x.isImpostor && category == RoleCategory.Impostors) || (x.isNeutral && category == RoleCategory.Neutrals) || (!x.isNeutral &&
                !x.isImpostor && category == RoleCategory.Crewmates)))
                {
                    Configurations.TryGetValue(role.roleId, out var option);
                    if (option == null || (role.roleId is RoleId.Immoralist or RoleId.Sidekick or RoleId.Pursuer or RoleId.BomberB) ||
                        RoleInfo.Complex.Any(x => x.Any(y => y.roleId == role.roleId))) continue;

                    if (option is CustomRoleOption roleOption)
                    {
                        if (roleOption.getSelection() == 10)
                        {
                            string numText = "x" + roleOption.count;
                            list100.Add(gui.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role, null), gui.HorizontalMargin(0.1f), gui.RawText(GUIAlignment.Left, maskedAttr, numText)));
                            AddAllAdditional(roleOption, list100);
                        }
                        else if (roleOption.getSelection() > 0)
                        {
                            string numText = $"x{roleOption.count} ({roleOption.getString()})";
                            listRandom.Add(gui.HorizontalHolder(GUIAlignment.Left, GetAssignableText(role, null), gui.HorizontalMargin(0.1f), gui.RawText(GUIAlignment.Left, maskedAttr, numText)));
                            AddAllAdditional(roleOption, listRandom);
                        }
                    }
                }
            }
            CheckRoles(list100, listRandom);

            if (list100.Count == 0 && listRandom.Count == 0) return GUIEmptyContext.Default;

            List<GUIContext> result = [gui.HorizontalMargin(2.2f), gui.RawText(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.DocumentTitle), ModTranslation.getString(categoryName).Bold())];
            if (list100.Count > 0) result.Add(gui.VerticalHolder(GUIAlignment.Center, [gui.RawText(GUIAlignment.Center, maskedAttr, "-100%-".Bold()), .. list100, gui.Margin(new(2f, 0.3f))]));
            if (listRandom.Count > 0) result.Add(gui.VerticalHolder(GUIAlignment.Center, [gui.RawText(GUIAlignment.Center, maskedAttr, ("-" + ModTranslation.getString("randomPercentRoles") + "-").Bold()), .. listRandom, gui.Margin(new(2f, 0.3f))]));
            return gui.VerticalHolder(GUIAlignment.Top, result);
        }

        string GetSpecialAssignmentString(RoleInfo roleInfo)
        {
            string specialAssignmentOption = null;
            if (roleInfo.roleId == RoleId.Swapper)
            {
                if (roleInfo.isImpostor) specialAssignmentOption = CustomOptionHolder.swapperIsImpRate.getString();
                else specialAssignmentOption = $"{(10 - CustomOptionHolder.swapperIsImpRate.getSelection()) * 10}%";
            }
            else if (roleInfo.roleId == RoleId.Yasuna) specialAssignmentOption = $"{(10 - CustomOptionHolder.yasunaIsImpYasunaRate.getSelection()) * 10}%";
            else if (roleInfo.roleId == RoleId.EvilYasuna) specialAssignmentOption = CustomOptionHolder.yasunaIsImpYasunaRate.getString();
            else if (roleInfo.roleId == RoleId.NiceGuesser) specialAssignmentOption = $"{(10 - CustomOptionHolder.guesserIsImpGuesserRate.getSelection()) * 10}%";
            else if (roleInfo.roleId == RoleId.EvilGuesser) specialAssignmentOption = CustomOptionHolder.guesserIsImpGuesserRate.getString();
            else if ((roleInfo.roleId is RoleId.NiceWatcher or RoleId.EvilWatcher) && CustomOptionHolder.watcherAssignEqually.getSelection() != 0)
            {
                if (roleInfo.roleId == RoleId.NiceWatcher) specialAssignmentOption = $"{(10 - CustomOptionHolder.watcherIsImpWatcherRate.getSelection()) * 10}%";
                else specialAssignmentOption = CustomOptionHolder.watcherIsImpWatcherRate.getString();
            }
            else if (roleInfo.roleId == RoleId.Shifter)
            {
                if (roleInfo.isNeutral) specialAssignmentOption = CustomOptionHolder.shifterIsNeutralRate.getString();
                else specialAssignmentOption = $"{(10 - CustomOptionHolder.shifterIsNeutralRate.getSelection()) * 10}%";
            }

            return specialAssignmentOption;
        }

        List<GUIContext> complex = [];
        GUIContext complexContext = gui.EmptyContext;
        foreach (var comp in RoleInfo.Complex)
        {
            if (comp.Count == 0) continue;
            Configurations.TryGetValue(comp[0].roleId, out var option);
            if (option == null || option.getSelection() == 0 || (option == CustomOptionHolder.guesserSpawnRate && TORMapOptions.gameMode == CustomGamemodes.Guesser)) continue;
            if (option is CustomRoleOption roleOption)
            {
                string text = "x" + roleOption.count.ToString();
                if (option.getSelection() < 10) text += $" ({roleOption.getString()})";
                complex.Add(gui.HorizontalHolder(GUIAlignment.Left, comp.Count > 1 ? new TORGUIText(GUIAlignment.Center, maskedAttr, new RawTextComponent(option.getName())) : GetAssignableText(comp[0]),
                    gui.HorizontalMargin(0.1f), gui.RawText(GUIAlignment.Left, maskedAttr, text)));
                if (comp.Count > 1) {
                    foreach (var item in comp)
                    {
                        string sAssignmentText = GetSpecialAssignmentString(item);
                        if (sAssignmentText != null) {
                            if (sAssignmentText != "0%")
                                complex.Add(gui.HorizontalHolder(GUIAlignment.Left, gui.HorizontalMargin(0.1f), GetAssignableText(item), gui.HorizontalMargin(0.1f), gui.RawText(GUIAlignment.Left, maskedAttr, sAssignmentText)));
                        }
                        else
                            complex.Add(gui.HorizontalHolder(GUIAlignment.Left, gui.HorizontalMargin(0.1f), GetAssignableText(item)));
                    }
                }
                complex.Add(gui.VerticalMargin(0.1f));
            }
        }

        if (complex.Count > 0)
        {
            complexContext = gui.VerticalHolder(GUIAlignment.Top, [gui.HorizontalMargin(2f),
                gui.RawText(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.DocumentTitle), ModTranslation.getString("complexRoles").Bold()),
                ..complex
                ]);
        }

        List<GUIContext> modifiers = [];
        GUIContext modifiersContext = gui.EmptyContext;
        foreach (var m in RoleInfo.allRoleInfos)
        {
            if (!m.isModifier) continue;
            Configurations.TryGetValue(m.roleId, out var option);
            if (option == null || option.getSelection() == 0) continue;
            string text = GameOptionsDataPatch.buildModifierExtras(option).Replace(" (", "").Replace(")", "");
            if (text.IsNullOrWhiteSpace()) text = "1";
            if (option.getSelection() < 10) text += $" ({option.getString()})";

            modifiers.Add(gui.HorizontalHolder(GUIAlignment.Left, GetAssignableText(m), gui.HorizontalMargin(0.1f), gui.RawText(GUIAlignment.Left, maskedAttr, "x" + text)));
        }

        if (modifiers.Count > 0)
        {
            modifiersContext = gui.VerticalHolder(GUIAlignment.Top, [gui.HorizontalMargin(2f),
                gui.RawText(GUIAlignment.Center, gui.GetAttribute(AttributeAsset.DocumentTitle), ModTranslation.getString("modifiers").Bold()),
                ..modifiers
                ]);
        }

        List<GUIContext> winConds = [
            gui.RawText(GUIAlignment.Left, maskedTitleAttr, ModTranslation.getString("winningConditionHintsAll")),
            ];

        foreach (var tip in EndGameConditionTips)
        {
            winConds.Add(gui.VerticalMargin(0.1f));
            winConds.Add(gui.HorizontalHolder(GUIAlignment.Left, gui.HorizontalMargin(0.2f), gui.RawText(GUIAlignment.Left, maskedSubtitleAttr, ModTranslation.getString(tip.Item1).Color(tip.Item2))));

            foreach (var detailTip in tip.Item3)
            {
                winConds.Add(gui.HorizontalHolder(GUIAlignment.Left, gui.HorizontalMargin(0.3f), gui.RawText(GUIAlignment.Left, maskedAttr, ("・" + ModTranslation.getString(detailTip + "EndGameTitle")).Bold())));
                winConds.Add(gui.HorizontalHolder(GUIAlignment.Left, gui.HorizontalMargin(0.45f), gui.RawText(GUIAlignment.Left, maskedAttr, ModTranslation.getString(detailTip))));
                winConds.Add(gui.VerticalMargin(0.15f));
            }
        }

        var view = new GUIScrollView(GUIAlignment.Center, new(7.4f, HelpHeight - 0.5f),
            gui.HorizontalHolder(GUIAlignment.Left, gui.HorizontalMargin(0.35f),
            gui.VerticalHolder(GUIAlignment.Center,
                gui.HorizontalHolder(GUIAlignment.Center,
                GetRoleOverview(RoleCategory.Impostors, "impostor"),
                GetRoleOverview(RoleCategory.Neutrals, "roleIntroNeutral"),
                GetRoleOverview(RoleCategory.Crewmates, "crewmate")),
                gui.HorizontalHolder(GUIAlignment.Center,
                complexContext,
                modifiersContext),
                gui.VerticalHolder(GUIAlignment.Left, winConds)
                )));

        return new MetaContextOld.WrappedContext(view);
    }

    private static GUIContext GetAssignableOverlay(RoleInfo assignable)
    {
        List<GUIContext> context = [];

        var gui = TORGUIContextEngine.API;

        context.Add(new TORGUIText(GUIAlignment.Left, gui.GetAttribute(AttributeAsset.OverlayTitle), new RawTextComponent(Helpers.cs(assignable.color, assignable.name))));
        context.Add(new TORGUIText(GUIAlignment.Left, gui.GetAttribute(AttributeAsset.OverlayContent), new RawTextComponent(assignable.blurb)));

        return new VerticalContextsHolder(GUIAlignment.Left, context);
    }

    static public GUIContext GetAchievementContext(RoleInfo assignable)
    {
        if (assignable == null) return TORGUIContextEngine.API.EmptyContext;

        var attr = new TextAttributes(TORGUIContextEngine.API.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.85f) };
        var headerAttr = new TextAttributes(TORGUIContextEngine.API.GetAttribute(AttributeParams.StandardLeft)) { FontSize = new(1.1f) };

        GUIContext AchievementTitleContext(Achievement a) => new HorizontalContextsHolder(GUIAlignment.Left,
                TORGUIContextEngine.API.Image(GUIAlignment.Left, new WrapSpriteLoader(() => Achievement.TrophySprite.GetSprite(a.Trophy)), new FuzzySize(0.38f, 0.38f)),
                TORGUIContextEngine.API.Margin(new(0.15f, 0.1f)),
                TORGUIContextEngine.API.VerticalHolder(GUIAlignment.Left,
                TORGUIContextEngine.API.VerticalMargin(0.12f),
                new TORGUIText(GUIAlignment.Left, headerAttr, a.GetHeaderComponent()),
                TORGUIContextEngine.API.VerticalMargin(-0.12f),
                new TORGUIText(GUIAlignment.Left, attr, a.GetTitleComponent(Achievement.HiddenComponent))
                {
                    OverlayContext = a.GetOverlayContext(showTitleInfo: true),
                    OnClickText = (() => { if (a.IsCleared) { Achievement.SetOrToggleTitle(a); } }, true)
                }
                ));

        var achievements = Achievement.allAchievements.Where(a => a.Category.HasValue && a.Category?.role == assignable && !a.IsHidden).ToArray();
        var context = achievements.Select(AchievementTitleContext);

        if (achievements.Length == 0) return TORGUIContextEngine.API.EmptyContext;

        return GetChapter("achievementTitle", [TORGUIContextEngine.API.VerticalHolder(GUIAlignment.Left, context)]);
    }

    static private GUIContext GetConfigurationsContext(RoleInfo assignable)
    {
        var allOptions = new List<string>();
        var option = Configurations.TryGetValue(assignable.roleId, out var assignmentOption);
        if (assignmentOption == null) return TORGUIContextEngine.API.EmptyContext;

        return TORGUIContextEngine.API.RawText(GUIAlignment.Left, TORGUIContextEngine.API.GetAttribute(AttributeAsset.DocumentStandard), optionsToString(assignmentOption));
    }

    private static MetaScreen OpenHelpScreen(HelpTab tab)
    {
        var screen = MetaScreen.GenerateWindow(new(7.8f, HelpHeight + 0.6f), HudManager.Instance.transform, Vector3.zero, true, false, background: BackgroundSetting.Modern);

        HelpTab validTabs = HelpTab.Achievements | HelpTab.Roles | HelpTab.Options | HelpTab.Overview;
        if (!(MapUtilities.CachedShipStatus == null || PlayerControl.LocalPlayer == null || HudManager.Instance == null || FastDestroyableSingleton<HudManager>.Instance.IsIntroDisplayed)) {
            validTabs |= HelpTab.MyInfo;
        }

        ShowScreen(screen, tab, validTabs);

        return screen;
    }

    static public List<(string, Color32, List<string>)> EndGameConditionTips =
    [
        ("impostorWin", Palette.ImpostorRed, ["impostorSaboWinHint", "impostorKillWinHint"]),
        ("crewWin", Color.white, ["crewmateTaskWinHint", "crewmateExileWinHint"]),
        ("jesterWin", Jester.color, ["jesterWinCondHint"]),
        ("arsonistWin", Arsonist.color, ["arsonistWinCondHint"]),
        ("jackalWin", Jackal.color, ["jackalWinCondHint"]),
        ("jekyllAndHydeWin", JekyllAndHyde.color, ["jekyllAndHydeKillWinHint", "jekyllAndHydeOutnumberWinHint"]),
        ("vultureWin", Vulture.color, ["vultureWinCondHint"]),
        ("lawyerWin", Lawyer.color, ["lawyerMeetingWinHint", "lawyerReplacementWinHint", "lawyerWinTogetherWinHint"]),
        ("loversWin", Lovers.color, ["loversSoloWinCondHint", "loversTeamWinCondHint"]),
        ("moriartyWin", Moriarty.color, ["moriartyKillWinHint", "moriartyOutnumberWinHint"]),
        ("doomsayerWin", Doomsayer.color, ["doomsayerWinCondHint"]),
        ("kataomoiWin", Kataomoi.color, ["kataomoiWinCondHint"]),
        ("plagueDoctorWin", PlagueDoctor.color, ["plagueDoctorWinCondHint"]),
        ("akujoWin", Akujo.color, ["akujoWinCondHint"]),
        ("foxWin", Fox.color, ["foxWinCondHint"])
    ];

    public static Dictionary<RoleId, CustomOption> Configurations = [];

    public static void Load()
    {
        Configurations = new()
            {
                { RoleId.Jester, CustomOptionHolder.jesterSpawnRate },
                { RoleId.Mayor, CustomOptionHolder.mayorSpawnRate },
                { RoleId.Portalmaker, CustomOptionHolder.portalmakerSpawnRate },
                { RoleId.Engineer, CustomOptionHolder.engineerSpawnRate },
                { RoleId.Sheriff, CustomOptionHolder.sheriffSpawnRate },
                { RoleId.Deputy, CustomOptionHolder.deputySpawnRate },
                { RoleId.Lighter, CustomOptionHolder.lighterSpawnRate },
                { RoleId.Godfather, CustomOptionHolder.mafiaSpawnRate },
                { RoleId.Mafioso, CustomOptionHolder.mafiaSpawnRate },
                { RoleId.Janitor, CustomOptionHolder.mafiaSpawnRate },
                { RoleId.Morphling, CustomOptionHolder.morphlingSpawnRate },
                { RoleId.Camouflager, CustomOptionHolder.camouflagerSpawnRate },
                { RoleId.Vampire, CustomOptionHolder.vampireSpawnRate },
                { RoleId.Eraser, CustomOptionHolder.eraserSpawnRate },
                { RoleId.Trickster, CustomOptionHolder.tricksterSpawnRate },
                { RoleId.Cleaner, CustomOptionHolder.cleanerSpawnRate },
                { RoleId.Warlock, CustomOptionHolder.warlockSpawnRate },
                { RoleId.BountyHunter, CustomOptionHolder.bountyHunterSpawnRate },
                { RoleId.Detective, CustomOptionHolder.detectiveSpawnRate },
                { RoleId.Bait, CustomOptionHolder.baitSpawnRate },
                { RoleId.TimeMaster, CustomOptionHolder.timeMasterSpawnRate },
                { RoleId.Medic, CustomOptionHolder.medicSpawnRate },
                { RoleId.Seer, CustomOptionHolder.seerSpawnRate },
                { RoleId.Hacker, CustomOptionHolder.hackerSpawnRate },
                { RoleId.Tracker, CustomOptionHolder.trackerSpawnRate },
                { RoleId.Snitch, CustomOptionHolder.snitchSpawnRate },
                { RoleId.Jackal, CustomOptionHolder.jackalSpawnRate },
                { RoleId.Sidekick, CustomOptionHolder.jackalSpawnRate },
                { RoleId.Spy, CustomOptionHolder.spySpawnRate },
                { RoleId.SecurityGuard, CustomOptionHolder.securityGuardSpawnRate },
                { RoleId.Arsonist, CustomOptionHolder.arsonistSpawnRate },
                { RoleId.NiceGuesser, CustomOptionHolder.guesserSpawnRate },
                { RoleId.EvilGuesser, CustomOptionHolder.guesserSpawnRate },
                { RoleId.NiceWatcher, CustomOptionHolder.watcherSpawnRate },
                { RoleId.EvilWatcher, CustomOptionHolder.watcherSpawnRate },
                { RoleId.Vulture, CustomOptionHolder.vultureSpawnRate },
                { RoleId.Medium, CustomOptionHolder.mediumSpawnRate },
                { RoleId.Lawyer, CustomOptionHolder.lawyerSpawnRate },
                { RoleId.Pursuer, CustomOptionHolder.lawyerSpawnRate },
                { RoleId.Witch, CustomOptionHolder.witchSpawnRate },
                { RoleId.Assassin, CustomOptionHolder.assassinSpawnRate },
                { RoleId.Thief, CustomOptionHolder.thiefSpawnRate },
                { RoleId.Ninja, CustomOptionHolder.ninjaSpawnRate },
                { RoleId.NekoKabocha, CustomOptionHolder.nekoKabochaSpawnRate },
                { RoleId.SerialKiller, CustomOptionHolder.serialKillerSpawnRate },
                { RoleId.EvilTracker, CustomOptionHolder.evilTrackerSpawnRate },
                { RoleId.Undertaker, CustomOptionHolder.undertakerSpawnRate },
                { RoleId.MimicK, CustomOptionHolder.mimicSpawnRate },
                { RoleId.MimicA, CustomOptionHolder.mimicSpawnRate },
                { RoleId.BomberA, CustomOptionHolder.bomberSpawnRate },
                { RoleId.BomberB, CustomOptionHolder.bomberSpawnRate },
                { RoleId.Swapper, CustomOptionHolder.swapperSpawnRate },
                { RoleId.EvilHacker, CustomOptionHolder.evilHackerSpawnRate },
                { RoleId.Trapper, CustomOptionHolder.trapperSpawnRate },
                { RoleId.Blackmailer, CustomOptionHolder.blackmailerSpawnRate },
                { RoleId.Yoyo, CustomOptionHolder.yoyoSpawnRate },
                { RoleId.FortuneTeller, CustomOptionHolder.fortuneTellerSpawnRate },
                { RoleId.Veteran, CustomOptionHolder.veteranSpawnRate },
                { RoleId.Sprinter, CustomOptionHolder.sprinterSpawnRate },
                { RoleId.Sherlock, CustomOptionHolder.sherlockSpawnRate },
                { RoleId.Yasuna, CustomOptionHolder.yasunaSpawnRate },
                { RoleId.TaskMaster, CustomOptionHolder.taskMasterSpawnRate },
                { RoleId.Teleporter, CustomOptionHolder.teleporterSpawnRate },
                { RoleId.Busker, CustomOptionHolder.buskerSpawnRate },
                { RoleId.Noisemaker, CustomOptionHolder.noisemakerSpawnRate },
                { RoleId.Archaeologist, CustomOptionHolder.archaeologistSpawnRate },
                { RoleId.EvilYasuna, CustomOptionHolder.yasunaSpawnRate },
                { RoleId.Opportunist, CustomOptionHolder.opportunistSpawnRate },
                { RoleId.Shifter, CustomOptionHolder.shifterSpawnRate },
                { RoleId.Moriarty, CustomOptionHolder.moriartySpawnRate },
                { RoleId.Akujo, CustomOptionHolder.akujoSpawnRate },
                { RoleId.PlagueDoctor, CustomOptionHolder.plagueDoctorSpawnRate },
                { RoleId.JekyllAndHyde, CustomOptionHolder.jekyllAndHydeSpawnRate },
                { RoleId.Cupid, CustomOptionHolder.cupidSpawnRate },
                { RoleId.Fox, CustomOptionHolder.foxSpawnRate },
                { RoleId.Immoralist, CustomOptionHolder.foxSpawnRate },
                { RoleId.SchrodingersCat, CustomOptionHolder.schrodingersCatSpawnRate },
                { RoleId.Kataomoi, CustomOptionHolder.kataomoiSpawnRate },
                { RoleId.Doomsayer, CustomOptionHolder.doomsayerSpawnRate },
                { RoleId.AntiTeleport, CustomOptionHolder.modifierAntiTeleport },
                { RoleId.Tiebreaker, CustomOptionHolder.modifierTieBreaker },
                { RoleId.Armored, CustomOptionHolder.modifierArmored },
                { RoleId.Bloody, CustomOptionHolder.modifierBloody },
                { RoleId.Sunglasses, CustomOptionHolder.modifierSunglasses },
                { RoleId.Vip, CustomOptionHolder.modifierVip },
                { RoleId.Chameleon, CustomOptionHolder.modifierChameleon },
                { RoleId.Mini, CustomOptionHolder.modifierMini },
                { RoleId.Lover, CustomOptionHolder.modifierLover },
                { RoleId.Invert, CustomOptionHolder.modifierInvert },
            };
    }

    private static void ShowScreen(MetaScreen screen, HelpTab tab, HelpTab validTabs)
    {
        MetaContextOld context = new();
        context.Append(GetTabsContext(screen, tab, validTabs));
        context.Append(new MetaContextOld.VerticalMargin(0.1f));

        switch (tab)
        {
            case HelpTab.Achievements:
                context.Append(ShowAchievementScreen());
                break;
            case HelpTab.Roles:
                context.Append(ShowAssignableScreen());
                break;
            case HelpTab.Options:
                context.Append(ShowOptionsScreen());
                break;
            case HelpTab.MyInfo:
                context.Append(ShowMyRolesSrceen());
                break;
            case HelpTab.Overview:
                context.Append(ShowPreviewScreen());
                break;
        }

        screen.SetContext(context);
    }

    [HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
    public static class HelpMenuKeybinds
    {
        public static void Postfix(KeyboardJoystick __instance)
        {
            ChatController cc = FastDestroyableSingleton<HudManager>.Instance.Chat;
            bool isOpen = cc != null && cc?.IsOpenOrOpening == true;
            if (Input.GetKeyDown(KeyCode.H) && !isOpen)
            {
                if ((PlayerControl.LocalPlayer && LobbyBehaviour.Instance && !ShipStatus.Instance) ||
                    (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started && !Minigame.Instance))
                    TryOpenHelpScreen(HelpTab.Roles);
            }
        }
    }
}
