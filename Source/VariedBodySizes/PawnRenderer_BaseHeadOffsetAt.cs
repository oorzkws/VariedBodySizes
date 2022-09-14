using HarmonyLib;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(PawnRenderer), "BaseHeadOffsetAt")]
public static class PawnRenderer_BaseHeadOffsetAt
{
    public static void Postfix(ref Vector3 __result)
    {
        if (Main.CurrentPawn == null)
        {
            return;
        }

        if (Main.CurrentComponent == null)
        {
            return;
        }

        var pawnSizeFactor = Main.CurrentComponent.GetVariedBodySize(Main.CurrentPawn);

        __result.z *= pawnSizeFactor;
    }
}