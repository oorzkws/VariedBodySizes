using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(Pawn), "BodySize", MethodType.Getter)]
public static class Pawn_BodySizePatch
{
    private static readonly TimedCache<float> statCache = new(360);

    public static float Postfix(float result, Pawn __instance)
    {
        if (!VariedBodySizesMod.instance.Settings.AffectRealBodySize)
            return result;

        // cached value, or calculate cache and return
        if (statCache.TryGet(__instance, out var pawnSize))
        {
            return pawnSize;
        }
        
        result *= (Main.CurrentComponent?.GetVariedBodySize(__instance) ?? 1.0f);

        // Babies won't fit in cribs if body size exceeds 0.25
        if (__instance.DevelopmentalStage == DevelopmentalStage.Baby)
        {
            result = Mathf.Min(result, 0.25f);
        }
        
        // Cache and return
        return statCache.SetAndReturn(__instance, result);
    }
}


[HarmonyPatch(typeof(Pawn), "HealthScale", MethodType.Getter)]
public static class Pawn_HealthScalePatch
{
    private static readonly TimedCache<float> statCache = new(3600);
    public static float Postfix(float result, Pawn __instance)
    {
        if (!VariedBodySizesMod.instance.Settings.AffectRealHealthScale||Main.CurrentComponent == null)
            return result;

        // cached value, or calculate cache and return
        if (statCache.TryGet(__instance, out var pawnSize))
        {
            return pawnSize;
        }
        
        result *= (Main.CurrentComponent?.GetVariedBodySize(__instance) ?? 1.0f);

        // Cache and return
        return statCache.SetAndReturn(__instance, result);
    }
}