using HarmonyLib;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
public static class PawnRenderer_RenderPawnInternal
{
    public static void Prefix(Pawn ___pawn)
    {
        Main.CurrentPawn = ___pawn;
    }

    public static void Postfix()
    {
        Main.CurrentPawn = null;
    }
}