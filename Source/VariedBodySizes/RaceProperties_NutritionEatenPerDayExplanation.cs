using HarmonyLib;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(RaceProperties), "NutritionEatenPerDayExplanation")]
public static class RaceProperties_NutritionEatenPerDayExplanation
{
    public static void Prefix(ref Pawn p, out float __state)
    {
        __state = p.def.race.baseHungerRate;

        if (!VariedBodySizesMod.instance.Settings.AffectRealHungerRate)
        {
            return;
        }

        if (Main.CurrentPawn == null)
        {
            return;
        }

        if (Main.CurrentComponent == null)
        {
            return;
        }

        var pawnSizeFactor = Main.CurrentComponent.GetVariedBodySize(Main.CurrentPawn);
        p.def.race.baseHungerRate = __state * pawnSizeFactor;
    }

    public static void Postfix(Pawn p, float __state)
    {
        p.def.race.baseHungerRate = __state;
    }
}