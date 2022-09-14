using HarmonyLib;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(GraphicMeshSet), "MeshAt")]
public static class GraphicMeshSet_MeshAt
{
    public static void Postfix(GraphicMeshSet __instance, ref Mesh __result, Rot4 rot)
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

        if (rot.AsInt == 3)
        {
            __result = MeshMakerPlanes.NewPlaneMesh(1.5f * pawnSizeFactor, true, true);
            return;
        }

        __result = MeshMakerPlanes.NewPlaneMesh(1.5f * pawnSizeFactor, false, true);
    }
}