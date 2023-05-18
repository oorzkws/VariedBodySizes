using Log = Verse.Log;

namespace VariedBodySizes;

/// <summary>
/// Contains logic that must be run after the DefDatabase is initialized (thus, StaticConstructorOnStartup)
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

        return (float)Math.Round(Rand.Range(sizeRange.min, sizeRange.max), 2);
    }

    public static void ResetAllCaches(Pawn pawn)
    {
        if (HarmonyPatches.FacialAnimation_GetHeadMeshSetPatch.HeadCache.TryGet(pawn, out _))
        {
            HarmonyPatches.FacialAnimation_GetHeadMeshSetPatch.HeadCache.Remove(pawn);
        }

        if (HarmonyPatches.PawnRenderer_GetBodyOverlayMeshSetPatch.OverlayCache.TryGet(pawn, out _))
        {
            HarmonyPatches.PawnRenderer_GetBodyOverlayMeshSetPatch.OverlayCache.Remove(pawn);
        }

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

