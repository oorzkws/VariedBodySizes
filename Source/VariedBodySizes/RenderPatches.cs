using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedType.Local
// ReSharper disable UnusedMember.Local

namespace VariedBodySizes;
public static class RenderPatches
{
    private static readonly bool hasVEF;
    private static readonly Type VEFGeneExtension = AccessTools.TypeByName("VanillaGenesExpanded.GeneExtension");
    private static readonly AccessTools.FieldRef<object, Vector2> VEFBodyScaleField;
    private static readonly AccessTools.FieldRef<object, Vector2> VEFHeadScaleField;

    private static readonly TimedCache<GraphicMeshSet> overlayCache = new(360);

    static RenderPatches()
    {
        hasVEF = ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core") && VEFGeneExtension != null;
        if (!hasVEF) return;
        VEFBodyScaleField = AccessTools.FieldRefAccess<Vector2>( "VanillaGenesExpanded.GeneExtension:bodyScaleFactor");
        VEFHeadScaleField = AccessTools.FieldRefAccess<Vector2>( "VanillaGenesExpanded.GeneExtension:headScaleFactor");
    }

    private static Vector2 GetVEScalarForPawn(Pawn pawn, bool isHead = false)
    {
        var baseScale = Vector2.one;
        if (!hasVEF || !ModsConfig.BiotechActive || !pawn.RaceProps.Humanlike || pawn.genes is not { } genes)
            return baseScale;
        
        foreach (var gene in genes.GenesListForReading)
        {
            if (gene.def.modExtensions == null) continue;
            foreach (var ext in gene.def.modExtensions)
            {
                if (ext.GetType() != VEFGeneExtension) continue;
                baseScale *= (isHead ? VEFHeadScaleField : VEFBodyScaleField).Invoke(ext);
            }
        }

        return baseScale;
    }

    private static Vector2 GetBaseScalarForPawn(Pawn pawn)
    {
        return (Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f) * Vector2.one;
    }

    private static Vector2 GetScalarForPawn(Pawn pawn, bool isHead = false)
    {
        return GetBaseScalarForPawn(pawn) * GetVEScalarForPawn(pawn, isHead);
    }

    private static GraphicMeshSet GetBodyOverlayMeshForPawn(GraphicMeshSet baseMesh, Pawn pawn)
    {
        var returnedMesh = overlayCache.Get(pawn);
        
        if (returnedMesh != null)
        {
            return returnedMesh;
        }
        else // Yeah it's redundant but it's readable
        {
            // I don't really understand this bit but I can't do rotations in my head. Credit to Allyina for figuring it out.
            var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2;
            var scalarVector = GetScalarForPawn(pawn);
            var scaledMesh = MeshPool.GetMeshSetForWidth(baseVector.x * scalarVector.x, baseVector.z * scalarVector.y);
            overlayCache.Set(pawn, scaledMesh);
            return scaledMesh;
        }
        
    }
    
    private static Vector2 returnModifiedDrawHeight(Vector2 vec, Pawn pawn)
    {
        vec *= Mathf.Sqrt(GetVEScalarForPawn(pawn, true).y * GetBaseScalarForPawn(pawn).y);
        return vec;
    }
    
    // Unconditional
    [HarmonyBefore("OskarPotocki.VFECore")]
    private static class PatchesUnconditional{
        
        [HarmonyPatch]
        [HarmonyDebug]
        private static class GraphicMeshSet_GetHumanlikeSetForPawnPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHeadSetForPawn");
                yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBodySetForPawn");
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
            {
                // store which method we are patching currently
                var isHead = original.Name == "GetHumanlikeHeadSetForPawn";
                // The stack before every ret will just be the mesh for the pawn
                // We can drop in before each ret, clear the stack and do our things.
                // We'll need a local to convert the vector to x,y though
                var localVec = generator.DeclareLocal(typeof(Vector2));
                return new CodeMatcher(instructions).MatchStartForward(
                        new CodeMatch(OpCodes.Ret))
                    .Repeat(matcher =>
                        matcher.InsertAndAdvance(
                            // Discard current stack
                            new CodeInstruction(OpCodes.Pop),
                            // Pawn to stack
                            new CodeInstruction(OpCodes.Ldarg_0),
                            // Add a bool to the stack - whether this is head or body
                            new CodeInstruction(OpCodes.Ldc_I4, isHead ? 1 : 0),
                            // Pawn scalar to stack
                            new CodeInstruction(OpCodes.Call,
                                AccessTools.Method("VariedBodySizes.RenderPatches:GetScalarForPawn")),
                            // Pawn to stack
                            new CodeInstruction(OpCodes.Ldarg_0),
                            // Multiply scalar by the current size
                            new CodeInstruction(OpCodes.Call,
                                AccessTools.Method("Verse.HumanlikeMeshPoolUtility:HumanlikeBodyWidthForPawn")),
                            new CodeInstruction(OpCodes.Call,
                                AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(Vector2), typeof(float)})),
                            // Get X, Y
                            new CodeInstruction(OpCodes.Stloc, localVec.LocalIndex),
                            new CodeInstruction(OpCodes.Ldloc, localVec.LocalIndex),
                            new CodeInstruction(CodeInstruction.LoadField(typeof(Vector2), "x")),
                            new CodeInstruction(OpCodes.Ldloc, localVec.LocalIndex),
                            new CodeInstruction(CodeInstruction.LoadField(typeof(Vector2), "y")),
                            // VEF will insert its own function, but does so destructively.
                            // We'll prevent that and recreate their logic just to be nice
                            new CodeInstruction(OpCodes.Call,
                                AccessTools.Method("Verse.MeshPool:GetMeshSetForWidth", new[] {typeof(float), typeof(float)}))
                        ).Advance(1) // Jump past the `ret` so we don't match it the next go
                    ).InstructionEnumeration();
            }
        }

        [HarmonyPatch]
        [HarmonyDebug]
        private static class GraphicMeshSet_GetHairBeardSetForPawnPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHairSetForPawn");
                yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBeardSetForPawn");
            }
            
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
                ILGenerator generator, MethodBase original)
            {
                // Adding vector = returnModifiedDrawHeight(vector, pawn);
                return new CodeMatcher(instructions).MatchStartForward(
                        new CodeMatch(OpCodes.Stloc_0))
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Stloc_0),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        //Add a bool to the stack - this uses the head, not body offset
                        new CodeInstruction(OpCodes.Ldc_I4, 0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method("VariedBodySizes.RenderPatches:GetScalarForPawn")),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Vector2), "op_Multiply", new []{typeof(Vector2), typeof(Vector2)}))
                    ).InstructionEnumeration();
            }
        }

        [HarmonyPatch(typeof(PawnRenderer), "GetBodyOverlayMeshSet")]
        //[HarmonyDebug]
        private static class PawnRenderer_GetBodyOverlayMeshSetPatch
        {
            static void Postfix(ref GraphicMeshSet __result, Pawn ___pawn)
            {
                __result = GetBodyOverlayMeshForPawn(__result, ___pawn);
            }
        }
        
        [HarmonyPatch(typeof(PawnRenderer), "BaseHeadOffsetAt")]
        //[HarmonyDebug]
        private static class PawnRenderer_BaseHeadOffsetAt_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // Just gonna search here for the switch statement and drop in before
                // Adding vector = returnModifiedDrawHeight(vector, pawn);
                return new CodeMatcher(instructions).MatchStartForward(
                        new CodeMatch(OpCodes.Stloc_0),
                        new CodeMatch(OpCodes.Ldarga_S),
                        new CodeMatch(OpCodes.Call),
                        new CodeMatch(OpCodes.Stloc_1),
                        new CodeMatch(OpCodes.Ldloc_1),
                        new CodeMatch(OpCodes.Switch))
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Stloc_0),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(CodeInstruction.LoadField(typeof(PawnRenderer), "pawn")),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method("VariedBodySizes.RenderPatches:returnModifiedDrawHeight"))
                    ).InstructionEnumeration();
            }
        }
    }
    
    // VEF only
    /*[HarmonyAfter("OskarPotocki.VFECore")]
    private static class PatchesWithVEF
    {

        [HarmonyPatch]
        private static class HumanlikeMeshPoolUtility_Patches_GeneScaleFactorPatch
        {
            private static MethodBase targetMethod;
            static bool Prepare()
            {
                if (!hasVEF) return false;
                targetMethod = AccessTools.Method("VanillaGenesExpanded.HumanlikeMeshPoolUtility_Patches:GeneScaleFactor");
                return targetMethod != null;
            }

            static MethodBase TargetMethod() => targetMethod;

            static void Postfix(Pawn pawn, ref float __result)
            {
                __result *= Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f;
            }
        }
    }*/
}