using HarmonyLib;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(Pawn), "HealthScale", MethodType.Getter)]
public static class Pawn_HealthScale
{
    public static void Postfix(ref float __result, Pawn __instance)
    {
        if (!VariedBodySizesMod.instance.Settings.AffectRealHealthScale)
        {
            return;
        }

        if (Main.CurrentComponent == null)
        {
            return;
        }

        __result *= Main.CurrentComponent.GetVariedBodySize(__instance);
    }
}