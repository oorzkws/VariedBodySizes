using HarmonyLib;
using RimWorld;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(Need_Food), "FoodFallPerTickAssumingCategory")]
public static class Need_Food_FoodFallPerTickAssumingCategory
{
    public static void Prefix(ref Pawn ___pawn, out float __state)
    {
        __state = ___pawn.def.race.baseHungerRate;

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
        ___pawn.def.race.baseHungerRate = __state * pawnSizeFactor;
    }

    public static void Postfix(Pawn ___pawn, float __state)
    {
        ___pawn.def.race.baseHungerRate = __state;
    }
}