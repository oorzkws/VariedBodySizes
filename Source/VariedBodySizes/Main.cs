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
    public static Pawn CurrentPawn;
    public static VariedBodySizes_GameComponent CurrentComponent;
    public static readonly List<ThingDef> AllPawnTypes;
    public static readonly Dictionary<float, Mesh> CachedMeshes;
    public static readonly Dictionary<float, Mesh> CachedInvertedMeshes;

    static Main()
    {
        AllPawnTypes = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.race != null)
            .OrderBy(def => def.label).ToList();
        CachedMeshes = new Dictionary<float, Mesh>();
        CachedInvertedMeshes = new Dictionary<float, Mesh>();
        new Harmony("Mlie.VariedBodySizes").PatchAll(Assembly.GetExecutingAssembly());
        if (VariedBodySizesMod.instance.Settings.VariedBodySizes == null)
        {
            VariedBodySizesMod.instance.Settings.VariedBodySizes = new Dictionary<string, FloatRange>();
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

    public static Mesh GetPawnMesh(float size, bool inverted)
    {
        if (inverted)
        {
            if (!CachedInvertedMeshes.ContainsKey(size))
            {
                CachedInvertedMeshes[size] = MeshMakerPlanes.NewPlaneMesh(1.5f * size, true, true);
            }

            return CachedInvertedMeshes[size];
        }

        if (!CachedMeshes.ContainsKey(size))
        {
            CachedMeshes[size] = MeshMakerPlanes.NewPlaneMesh(1.5f * size, false, true);
        }

        return CachedMeshes[size];
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