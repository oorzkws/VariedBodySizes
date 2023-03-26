using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VariedBodySizes;

[StaticConstructorOnStartup]
public static class Main
{
    public static VariedBodySizes_GameComponent CurrentComponent;
    public static readonly List<ThingDef> AllPawnTypes;
    public static readonly Harmony HarmonyInstance;

    static Main()
    {
        AllPawnTypes = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.race != null)
            .OrderBy(def => def.label).ToList();
        HarmonyInstance = new Harmony("Mlie.VariedBodySizes");
        // Until VEF changes their mind we're just going to override with our own scaling.
        foreach (var targetPair in new[]
                 {
                     new KeyValuePair<Type, string>(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeBodySetForPawn"),
                     new KeyValuePair<Type, string>(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeHeadSetForPawn"),
                     //new(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeHairSetForPawn"),
                     //new(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeBeardSetForPawn"),
                     //new(typeof(HumanlikeMeshPoolUtility), "HumanlikeBodyWidthForPawn"),
                     //new(typeof(PawnRenderer), "GetBodyOverlayMeshSet"),
                     new(typeof(PawnRenderer), "BaseHeadOffsetAt"),
                     //new(typeof(PawnRenderer), "DrawBodyApparel"),
                     //new(typeof(PawnRenderer), "DrawBodyGenes"),
                     //new(typeof(GeneGraphicData), "GetGraphics"),
                     //new(AccessTools.TypeByName("Verse.PawnRenderer+<>c__DisplayClass54_0"),
                     //    "<DrawHeadHair>g__DrawExtraEyeGraphic|6")
                 })
        {
            var targetMethod = AccessTools.Method(targetPair.Key, targetPair.Value);
            if (targetMethod == null)
            {
                continue;
            }

            var patches = Harmony.GetPatchInfo(targetMethod);
            if (patches?.Owners.Contains("OskarPotocki.VFECore") != true)
            {
                continue;
            }

            LogMessage($"Unpatching {targetMethod.DeclaringType?.Name ?? string.Empty}:{targetMethod.Name}", true);
            HarmonyInstance.Unpatch(targetMethod, HarmonyPatchType.All, "OskarPotocki.VFECore");
        }

        // Do our patches after we undo theirs
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        VariedBodySizesMod.instance.Settings.VariedBodySizes ??= new Dictionary<string, FloatRange>();
    }

    public static float GetPawnVariation(Pawn pawn)
    {
        var sizeRange = VariedBodySizesMod.instance.Settings.DefaultVariation;
        if (VariedBodySizesMod.instance.Settings.VariedBodySizes.ContainsKey(pawn.def.defName))
        {
            sizeRange = VariedBodySizesMod.instance.Settings.VariedBodySizes[pawn.def.defName];
        }

        return (float)Math.Round(Rand.Range(sizeRange.min, sizeRange.max), 2);
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

public static partial class HarmonyPatches
{
    // We have to overwrite their patches, unfortunately
    private static bool hasVef => ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core");

    private static float GetScalarForPawn(Pawn pawn)
    {
        return Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f;
    }

    private static bool NotNull(params object[] input)
    {
        if (input.All(o => o is not null))
        {
            return true;
        }

        Main.LogMessage("Signature match not found", true);
        foreach (var obj in input)
        {
            if (obj is MemberInfo memberObj)
            {
                Main.LogMessage($"\tValid entry:{memberObj}", true);
            }
        }

        return false;
    }

    private static IEnumerable<MethodBase> YieldAll(params MethodBase[] input)
    {
        return input;
    }

    // CodeMatcher will throw errors if we try to take actions in an invalid state (i.e. no match)
    private static void OnSuccess(CodeMatcher match, Action<CodeMatcher> action)
    {
        if (match.IsInvalid)
        {
            Main.LogMessage($"Transpiler did not find target @ {new StackTrace().GetFrame(1).GetMethod().DeclaringType?.FullName ?? "unknown"}", true);
            return;
        }

        action.Invoke(match);
    }
}