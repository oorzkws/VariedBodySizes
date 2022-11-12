using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace VariedBodySizes;

[HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
public static class PawnRenderer_RenderPawnInternal
{
    private static List<Pawn> ModifiedPawns = new();
    
    private static IEnumerable<Graphic> GraphicSetIterator(Pawn pawn, PawnGraphicSet graphicSet)
    {
        
        if (!graphicSet.AllResolved)
        {
            graphicSet.ResolveAllGraphics();
        }
        
        // Apparel
        if (graphicSet.apparelGraphics != null)
            foreach (var apparelGraphicRecord in graphicSet.apparelGraphics)
            {
                yield return apparelGraphicRecord.graphic;
            }

        // Genes
        if (graphicSet.apparelGraphics != null)
            foreach (var geneGraphicRecord in graphicSet.geneGraphics)
            {
                yield return geneGraphicRecord.graphic;
            }

        yield return graphicSet.nakedGraphic;
        yield return graphicSet.rottingGraphic;
        yield return graphicSet.dessicatedGraphic;
        yield return graphicSet.corpseGraphic;
        yield return graphicSet.packGraphic;
        yield return graphicSet.headGraphic;
        yield return graphicSet.desiccatedHeadGraphic;
        yield return graphicSet.skullGraphic;
        yield return graphicSet.headStumpGraphic;
        yield return graphicSet.desiccatedHeadStumpGraphic;
        yield return graphicSet.hairGraphic;
        yield return graphicSet.beardGraphic;
        yield return graphicSet.swaddledBabyGraphic;
        yield return graphicSet.bodyTattooGraphic;
        yield return graphicSet.faceTattooGraphic;
        yield return graphicSet.furCoveredGraphic;

    }

    public static void Prefix(ref Pawn ___pawn, ref PawnRenderer __instance, out Dictionary<Graphic,Vector2> __state)
    {
        Main.CurrentPawn = ___pawn;

        __state = new Dictionary<Graphic, Vector2>();
        
        var pawnSize = Main.CurrentComponent.GetVariedBodySize(___pawn);
        // Process
        foreach (var record in GraphicSetIterator(___pawn, __instance.graphics))
        {
            if (record == null) continue;
            if (__state.ContainsKey(record)) continue;
            __state.Add(record, new Vector2(record.drawSize.x, record.drawSize.y));
            Log.Warning($"{record.path}: {record.drawSize} -> {record.drawSize * pawnSize}");
            record.drawSize *= pawnSize;
        }

    }

    public static void Postfix(ref Pawn ___pawn, ref PawnRenderer __instance, Dictionary<Graphic,Vector2> __state)
    {
        foreach (var pair in __state)
        {
            pair.Key.drawSize = pair.Value;
            __state.Remove(pair.Key);
        }
        Main.CurrentPawn = null;
    }
}

[HarmonyPatch(typeof(PawnGraphicSet), "ResolveAllGraphics")]
public static class PawnGraphicSet_ResolveAllGraphicsPatch
{
    public static void Postfix(PawnGraphicSet __instance)
    {
        if(Main.CurrentComponent == null) return;
        if (__instance.pawn == null) return;
    }
}