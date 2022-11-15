/*using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

// VE breaks pawn render scaling with transpiler fun times so we have to modify the meshes that get passed to it...
//[HarmonyPatch]
public static class VEF_DrawSettings_TryGetNewMeshPatch
{
    private static readonly string[] VEFMethods =
    {
        "VFECore.Patch_PawnRenderer_DrawPawnBody_Transpiler:ModifyMesh",
        "VFECore.Patch_DrawHeadHair_DrawApparel_Transpiler:TryModifyMeshRef",
        "VFECore.Harmony_PawnRenderer_DrawBodyApparel:ModifyShellMesh",
        "VFECore.Harmony_PawnRenderer_DrawBodyApparel:ModifyPackMesh"
    };

    public static bool Prepare()
    {
        foreach (var methodPath in VEFMethods)
        {
            var targetMethod = AccessTools.Method(methodPath);
            if (targetMethod == null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var methodPath in VEFMethods)
        {
            var targetMethod = AccessTools.Method(methodPath);
            if (targetMethod == null)
            {
                continue;
            }

            yield return targetMethod;
        }
    }

    public static void Prefix(ref Pawn pawn, ref Mesh mesh)
    {
        if (Main.CurrentPawn == null)
        {
            return;
        }

        if (Main.CurrentComponent == null)
        {
            return;
        }

        if (pawn.RaceProps is { Humanlike: true })
        {
            mesh = Main.GetPawnMesh(Main.CurrentComponent.GetVariedBodySize(pawn), pawn.Rotation.AsInt == 3);
        }
    }
}*/