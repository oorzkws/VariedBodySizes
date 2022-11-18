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

    static Main()
    {
        AllPawnTypes = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.race != null)
            .OrderBy(def => def.label).ToList();
        new Harmony("Mlie.VariedBodySizes").PatchAll(Assembly.GetExecutingAssembly());
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