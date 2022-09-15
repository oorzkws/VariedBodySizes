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

        __result = Main.GetPawnMesh(Main.CurrentComponent.GetVariedBodySize(Main.CurrentPawn), rot.AsInt == 3);
    }
}