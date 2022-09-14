using System.Collections.Generic;
using System.Linq;
using Mlie;
using RimWorld;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

[StaticConstructorOnStartup]
internal class VariedBodySizesMod : Mod
{
    /// <summary>
    ///     The instance of the settings to be read by the mod
    /// </summary>
    public static VariedBodySizesMod instance;

    private static string currentVersion;
    private static readonly Vector2 searchSize = new Vector2(200f, 25f);
    private static readonly Vector2 buttonSize = new Vector2(120f, 25f);
    private static readonly Vector2 iconSize = new Vector2(58f, 58f);
    private static string searchText;
    private static Vector2 scrollPosition;
    private static readonly Color alternateBackground = new Color(0.2f, 0.2f, 0.2f, 0.5f);


    /// <summary>
    ///     The private settings
    /// </summary>
    private VariedBodySizesModSettings settings;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="content"></param>
    public VariedBodySizesMod(ModContentPack content)
        : base(content)
    {
        instance = this;
        searchText = string.Empty;
        currentVersion =
            VersionFromManifest.GetVersionFromModMetaData(
                ModLister.GetActiveModWithIdentifier("Mlie.VariedBodySizes"));
    }

    /// <summary>
    ///     The instance-settings for the mod
    /// </summary>
    internal VariedBodySizesModSettings Settings
    {
        get
        {
            if (settings == null)
            {
                settings = GetSettings<VariedBodySizesModSettings>();
            }

            return settings;
        }

        set => settings = value;
    }

    public override string SettingsCategory()
    {
        return "Varied Body Sizes";
    }

    /// <summary>
    ///     The settings-window
    /// </summary>
    /// <param name="rect"></param>
    public override void DoSettingsWindowContents(Rect rect)
    {
        base.DoSettingsWindowContents(rect);

        var listing_Standard = new Listing_Standard();
        listing_Standard.Begin(rect);
        var defaultLabel = listing_Standard.Label("VariedBodySizes.defaultvariation.label".Translate());
        var defaultRangeRect = new Rect(defaultLabel.position + new Vector2(rect.width / 2, 0),
            new Vector2(defaultLabel.width / 2, defaultLabel.height));
        Widgets.FloatRange(defaultRangeRect, "DefaultVariation".GetHashCode(), ref Settings.DefaultVariation, 0.25f, 2f,
            null, ToStringStyle.PercentOne);
        if (Current.Game != null && Main.CurrentComponent != null)
        {
            var resetLabel = listing_Standard.Label("VariedBodySizes.resetgame".Translate());
            if (Widgets.ButtonText(
                    new Rect(resetLabel.position + new Vector2(resetLabel.width - buttonSize.x, 0),
                        buttonSize),
                    "VariedBodySizes.reset".Translate()))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "VariedBodySizes.resetgame.dialog".Translate(),
                    "VariedBodySizes.no".Translate(), null, "VariedBodySizes.yes".Translate(),
                    delegate
                    {
                        Main.CurrentComponent.VariedBodySizesDictionary = new Dictionary<Pawn, float>();
                        Current.Game.CurrentMap.mapPawns.AllPawns.ForEach(delegate(Pawn pawn)
                        {
                            PortraitsCache.SetDirty(pawn);
                            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
                        });
                    }));
            }
        }

        listing_Standard.CheckboxLabeled("VariedBodySizes.logging.label".Translate(), ref Settings.VerboseLogging,
            "VariedBodySizes.logging.tooltip".Translate());
        listing_Standard.CheckboxLabeled("VariedBodySizes.realbodysize.label".Translate(),
            ref Settings.AffectRealBodySize,
            "VariedBodySizes.realbodysize.tooltip".Translate());

        if (currentVersion != null)
        {
            listing_Standard.Gap();
            GUI.contentColor = Color.gray;
            listing_Standard.Label("VariedBodySizes.version.label".Translate(currentVersion));
            GUI.contentColor = Color.white;
        }

        listing_Standard.GapLine();
        Text.Font = GameFont.Medium;
        var titleRect = listing_Standard.Label("VariedBodySizes.variations.label".Translate());
        Text.Font = GameFont.Small;
        if (Widgets.ButtonText(
                new Rect(titleRect.position + new Vector2(titleRect.width - buttonSize.x, 0), buttonSize),
                "VariedBodySizes.reset".Translate()))
        {
            Settings.ResetSettings();
        }

        var searchRect = listing_Standard.GetRect(searchSize.x);
        searchText =
            Widgets.TextField(
                new Rect(
                    searchRect.position +
                    new Vector2(searchRect.width - searchSize.x, 5),
                    searchSize),
                searchText);
        Widgets.Label(searchRect, "VariedBodySizes.search".Translate());

        var allPawnTypes = Main.AllPawnTypes;
        if (!string.IsNullOrEmpty(searchText))
        {
            allPawnTypes = Main.AllPawnTypes.Where(def =>
                    def.label.ToLower().Contains(searchText.ToLower()) || def.modContentPack?.Name.ToLower()
                        .Contains(searchText.ToLower()) == true)
                .ToList();
        }

        listing_Standard.End();

        var borderRect = rect;
        borderRect.height -= searchRect.y + 40f;
        borderRect.y += searchRect.y + 40f;
        var scrollContentRect = borderRect;
        scrollContentRect.height = allPawnTypes.Count * 61f;
        scrollContentRect.width -= 20;
        scrollContentRect.x = 0;
        scrollContentRect.y = 0;

        var scrollListing = new Listing_Standard();
        Widgets.BeginScrollView(borderRect, ref scrollPosition, scrollContentRect);
        scrollListing.Begin(scrollContentRect);
        var alternate = false;
        foreach (var pawnType in allPawnTypes)
        {
            var modInfo = pawnType.modContentPack?.Name;
            var rowRect = scrollListing.GetRect(60);
            alternate = !alternate;
            if (alternate)
            {
                Widgets.DrawBoxSolid(rowRect.ExpandedBy(10, 0), alternateBackground);
            }

            var currentValue = Settings.DefaultVariation;
            var originalColor = GUI.contentColor;
            if (instance.Settings.VariedBodySizes.ContainsKey(pawnType.defName))
            {
                currentValue = instance.Settings.VariedBodySizes[pawnType.defName];
                GUI.contentColor = Color.green;
            }

            var raceLabel = $"{pawnType.label.CapitalizeFirst()} ({pawnType.defName}) - {modInfo}";
            DrawIcon(pawnType,
                new Rect(rowRect.position, iconSize));
            var nameRect = new Rect(rowRect.position + new Vector2(iconSize.x, 3f),
                rowRect.size - new Vector2(iconSize.x, (rowRect.height / 2) + 3f));
            var sliderRect = new Rect(rowRect.position + new Vector2(iconSize.x, rowRect.height / 2),
                rowRect.size - new Vector2(iconSize.x, (rowRect.height / 2) + 3f));

            Widgets.Label(nameRect, raceLabel);
            Widgets.FloatRange(sliderRect, pawnType.defName.GetHashCode(), ref currentValue, 0.25f, 2f, null,
                ToStringStyle.PercentOne);
            GUI.contentColor = originalColor;
            if (currentValue != Settings.DefaultVariation)
            {
                instance.Settings.VariedBodySizes[pawnType.defName] = currentValue;
                continue;
            }

            if (instance.Settings.VariedBodySizes.ContainsKey(pawnType.defName))
            {
                instance.Settings.VariedBodySizes.Remove(pawnType.defName);
            }
        }

        scrollListing.End();
        Widgets.EndScrollView();
    }

    private void DrawIcon(ThingDef pawn, Rect rect)
    {
        rect = rect.ContractedBy(3f);
        var pawnKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawn.defName);
        if (pawnKind == null)
        {
            TooltipHandler.TipRegion(rect, $"{pawn.LabelCap}\n{pawn.description}");
            GUI.DrawTexture(rect, BaseContent.BadTex);
            return;
        }

        Widgets.DefIcon(rect, pawnKind);
        TooltipHandler.TipRegion(rect, $"{pawnKind.LabelCap}\n{pawnKind.race?.description}");
    }
}