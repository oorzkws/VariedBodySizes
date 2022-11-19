using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
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
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        VariedBodySizesMod.instance.Settings.VariedBodySizes ??= new Dictionary<string, FloatRange>();
        foreach (var targetPair in new KeyValuePair<Type, string>[]{
            new(typeof(HumanlikeMeshPoolUtility),"GetHumanlikeBodySetForPawn"),
            new(typeof(HumanlikeMeshPoolUtility),"GetHumanlikeHeadSetForPawn"),
            new(typeof(HumanlikeMeshPoolUtility),"GetHumanlikeHairSetForPawn"),
            new(typeof(HumanlikeMeshPoolUtility),"GetHumanlikeBeardSetForPawn"),
            new(typeof(PawnRenderer),"GetBodyOverlayMeshSet"),
            new(typeof(PawnRenderer),"BaseHeadOffsetAt")
        })
        {
            var targetMethod = AccessTools.Method(targetPair.Key, targetPair.Value);
            if (targetMethod != null)
            {
                var patches = Harmony.GetPatchInfo(targetMethod);
                if (patches is null)
                {
                    continue;
                }
                if (!patches.Owners.Contains("OskarPotocki.VFECore"))
                {
                    continue;
                }
                //FileLog.Log($"Unpatching {targetMethod.DeclaringType?.Name ?? string.Empty}:{targetMethod.Name}");
                //HarmonyInstance.Unpatch(targetMethod, HarmonyPatchType.All, "OskarPotocki.VFECore");
            }
        }
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