using HarmonyLib;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(Pawn), "BodySize", MethodType.Getter)]
public static class Pawn_BodySize
{
    public static void Postfix(ref float __result, Pawn __instance)
    {
        if (!VariedBodySizesMod.instance.Settings.AffectRealBodySize)
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