using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(Graphic), "MeshAt")]
public static class Graphic_MeshAt
{
    public static void Prefix(ref Vector2 ___drawSize, out Vector2 __state)
    {
        __state = ___drawSize;
        if (Main.CurrentPawn == null)
        {
            return;
        }

        if (Main.CurrentComponent == null)
        {
            return;
        }

        var pawnSizeFactor = Main.CurrentComponent.GetVariedBodySize(Main.CurrentPawn);
        ___drawSize = new Vector2(___drawSize.x * pawnSizeFactor, ___drawSize.y * pawnSizeFactor);
    }

    public static void Postfix(ref Vector2 ___drawSize, Vector2 __state)
    {
        ___drawSize = __state;
    }
}