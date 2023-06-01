using Log = Verse.Log;

namespace VariedBodySizes;

/// <summary>
///     Contains logic that must be run after the DefDatabase is initialized (thus, StaticConstructorOnStartup)
/// </summary>
[StaticConstructorOnStartup]
public static class Main
{
    public static VariedBodySizes_GameComponent CurrentComponent;
    public static readonly List<ThingDef> AllPawnTypes;

    static Main()
    {
        AllPawnTypes = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.race != null)
            .OrderBy(def => def.label).ToList();
        HarmonyPatches.ApplyAll(new Harmony("Mlie.VariedBodySizes"));
    }

    public static float GetPawnVariation(Pawn pawn)
    {
        var sizeRange = VariedBodySizesMod.instance.Settings.DefaultVariation;
        if (VariedBodySizesMod.instance.Settings.VariedBodySizes.TryGetValue(pawn.def.defName, out var bodySize))
        {
            sizeRange = bodySize;
        }

        var randomStandardNormal = Math.Sqrt(-2.0 * Math.Log(Rand.Value)) * Math.Sin(2.0 * Math.PI * Rand.Value);
        var mean = (sizeRange.min + sizeRange.max) / 2;
        var standardDeviation = (sizeRange.max - sizeRange.min) /
                                VariedBodySizesMod.instance.Settings.StandardDeviationDivider;
        return (float)Math.Round(mean + (standardDeviation * randomStandardNormal), 2);
    }

    public static void ResetAllCaches(Pawn pawn)
    {
        HarmonyPatches.FacialAnimation_GetHeadMeshSetPatch.HeadCache.Remove(pawn);
        HarmonyPatches.PawnRenderer_GetBodyOverlayMeshSetPatch.OverlayCache.Remove(pawn);
        CurrentComponent.sizeCache.Remove(pawn);
        GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
    }

    public static void LogMessage(string message, bool forced = false)
    {
        if (!forced && !VariedBodySizesMod.instance.Settings.VerboseLogging)
        {
            return;
        }

        Log.Message($"[VariedBodySizes]: {message}");
    }
}

// Utility stuff here