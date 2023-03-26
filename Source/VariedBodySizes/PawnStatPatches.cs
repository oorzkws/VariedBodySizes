using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn), "BodySize", MethodType.Getter)]
    public static class Pawn_BodySizePatch
    {
        private static readonly TimedCache<float> statCache = new TimedCache<float>(36);


        public static void Postfix(ref float result, Pawn __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealBodySize)
            {
                return;
            }

            // cached value, or calculate, cache and return
            if (statCache.TryGet(__instance, out var pawnSize) && Math.Abs(pawnSize - result) > 0.01f)
            {
                result = pawnSize;
                return;
            }

            result *= GetScalarForPawn(__instance);

            // Babies won't fit in cribs if body size exceeds 0.25
            if (__instance.DevelopmentalStage == DevelopmentalStage.Baby)
            {
                result = Mathf.Min(result, 0.25f);
            }

            // Cache and return
            statCache.Set(__instance, result);
        }
    }

    [HarmonyPatch(typeof(Pawn), "HealthScale", MethodType.Getter)]
    public static class Pawn_HealthScalePatch
    {
        private static readonly TimedCache<float> statCache = new TimedCache<float>(3600);


        public static void Postfix(ref float result, Pawn __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealHealthScale)
            {
                return;
            }

            // cached value, or calculate cache and return
            if (statCache.TryGet(__instance, out var pawnSize) && Math.Abs(pawnSize - result) > 0.01f)
            {
                result = pawnSize;
                return;
            }

            result *= GetScalarForPawn(__instance);

            // Cache and return
            statCache.Set(__instance, result);
        }
    }

    [HarmonyPatch(typeof(Need_Food), "FoodFallPerTickAssumingCategory")]
    public static class Need_Food_FoodFallPerTickAssumingCategoryPatch
    {
        public static void Postfix(ref float result, Pawn ___pawn)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealHungerRate)
            {
                return;
            }

            result *= GetScalarForPawn(___pawn);
        }
    }

    [HarmonyPatch(typeof(RaceProperties), "NutritionEatenPerDayExplanation")]
    public static class RaceProperties_NutritionEatenPerDayExplanationPatch
    {
        public static void Prefix(Pawn p, out float __state)
        {
            __state = float.NaN;
            if (!VariedBodySizesMod.instance.Settings.AffectRealHungerRate)
            {
                return;
            }

            __state = p.def.race.baseHungerRate;
            p.def.race.baseHungerRate *= GetScalarForPawn(p);
        }


        public static void Postfix(Pawn p, float __state)
        {
            if (float.IsNaN(__state))
            {
                return;
            }

            p.def.race.baseHungerRate = __state;
        }
    }

    [HarmonyPatch]
    public static class CompHasGatherableBodyResource_ResourceAmountPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var subClassType in typeof(CompHasGatherableBodyResource).AllSubclassesNonAbstract())
            {
                yield return AccessTools.PropertyGetter(subClassType, "ResourceAmount");
            }
        }

        public static void Postfix(ref int __result, ThingWithComps ___parent)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectHarvestYield)
            {
                return;
            }

            if (___parent is not Pawn pawn)
            {
                return;
            }

            __result = (int)Math.Round(__result * GetScalarForPawn(pawn));
        }
    }
}