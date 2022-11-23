using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static partial class HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn), "BodySize", MethodType.Getter)]
    [UsedImplicitly]
    public static class Pawn_BodySizePatch
    {
        private static readonly TimedCache<float> statCache = new(360);

        [UsedImplicitly]
        public static float Postfix(float result, Pawn __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealBodySize)
                return result;

            // cached value, or calculate cache and return
            if (statCache.TryGet(__instance, out var pawnSize))
            {
                return pawnSize;
            }

            result *= GetScalarForPawn(__instance);

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
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class Pawn_HealthScalePatch
    {
        private static readonly TimedCache<float> statCache = new(3600);

        [UsedImplicitly]
        public static float Postfix(float result, Pawn __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealHealthScale)
                return result;

            // cached value, or calculate cache and return
            if (statCache.TryGet(__instance, out var pawnSize))
            {
                return pawnSize;
            }

            result *= GetScalarForPawn(__instance);

            // Cache and return
            return statCache.SetAndReturn(__instance, result);
        }
    }

    [HarmonyPatch(typeof(Need_Food), "FoodFallPerTickAssumingCategory")]
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class Need_Food_FoodFallPerTickAssumingCategoryPatch
    {
        [UsedImplicitly]
        public static float Postfix(float result, Pawn ___pawn)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealHungerRate)
                return result;

            return result * GetScalarForPawn(___pawn);
        }
    }

    [HarmonyPatch(typeof(RaceProperties), "NutritionEatenPerDayExplanation")]
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class RaceProperties_NutritionEatenPerDayExplanationPatch
    {
        [UsedImplicitly]
        public static void Prefix(Pawn p, out float __state)
        {
            __state = float.NaN;
            if (!VariedBodySizesMod.instance.Settings.AffectRealHungerRate)
                return;

            __state = p.def.race.baseHungerRate;
            p.def.race.baseHungerRate *= GetScalarForPawn(p);
        }

        [UsedImplicitly]
        public static void Postfix(Pawn p, float __state)
        {
            if (float.IsNaN(__state))
                return;

            p.def.race.baseHungerRate = __state;
        }
    }
}