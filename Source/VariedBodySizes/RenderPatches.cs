using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using CodeInstruction = HarmonyLib.CodeInstruction;

// ReSharper disable InconsistentNaming

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
        
        foreach (var gene in genes.GenesListForReading.Where(g => g.Active))
        {
            if (gene.def.modExtensions == null) continue;
            foreach (var ext in gene.def.modExtensions.Where(e => e.GetType() == VEFGeneExtension))
            {
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
        
        if (returnedMesh != null && string.Empty != "")
        {
            return returnedMesh;
        }
        else // Yeah it's redundant but it's readable
        {
            var tempMesh = baseMesh.MeshAt(Rot4.North);
            Log.Warning($"Mesh for {pawn.Name}");
            foreach (var vert in tempMesh.vertices)
            {
                Log.Warning($"{vert.x},{vert.y},{vert.z}@{vert.magnitude}|{vert.sqrMagnitude}");
            }
            // I don't really understand this bit but I can't do rotations in my head. Credit to Allyina for figuring it out.
            var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2;
            var scalarVector = GetScalarForPawn(pawn);
            var scaledMesh = MeshPool.GetMeshSetForWidth(baseVector.x * scalarVector.x, baseVector.z * scalarVector.y);
            overlayCache.Set(pawn, scaledMesh);
            return scaledMesh;
        }
        
    }
  
    // Unconditional
    [HarmonyBefore("OskarPotocki.VFECore")]
    private static class PatchesUnconditional
    {

        // Verse.HumanlikeMeshPoolUtility
        private static readonly MethodBase getHeadSet =
            AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHeadSetForPawn");
        private static readonly MethodBase getBodySet =
            AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBodySetForPawn");
        private static readonly MethodBase getHairSet =
            AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHairSetForPawn");
        private static readonly MethodBase getBeardSet =
            AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBeardSetForPawn");
        
        // Verse.MeshPool
        private static readonly MethodBase getMeshSet1D =
            AccessTools.Method("Verse.MeshPool:GetMeshSetForWidth", new[] {typeof(float)});
        private static readonly MethodBase getMeshSet2D =
            AccessTools.Method("Verse.MeshPool:GetMeshSetForWidth", new[] {typeof(float), typeof(float)});

        private static readonly FieldInfo headSetField = AccessTools.Field("Verse.MeshPool:humanlikeHeadSet");
        private static readonly FieldInfo bodySetField = AccessTools.Field("Verse.MeshPool:humanlikeBodySet");

        // Verse.PawnRenderer
        private static readonly MethodBase headOffsetAt = AccessTools.Method("Verse.PawnRenderer:BaseHeadOffsetAt");
        private static readonly FieldInfo renderPawn = AccessTools.Field("Verse.PawnRenderer:pawn");
        private static readonly MethodBase drawBodyGenes = AccessTools.Method("Verse.PawnRenderer:DrawBodyGenes");


        // LifeStageDef
        private static readonly FieldInfo ageBodyFactor = AccessTools.Field("RimWorld.LifeStageDef:bodySizeFactor");
        
        // BodyTypeDef
        private static readonly FieldInfo bodyScale = AccessTools.Field("RimWorld.BodyTypeDef:bodyGraphicScale");

        // Vector2
        private static readonly MethodBase vector2TimesVector2 =
            AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(Vector2), typeof(Vector2)});
        private static readonly MethodBase floatTimesVector2 =
            AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(float), typeof(Vector2)});
        private static readonly MethodBase vector2TimesFloat =
            AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(Vector2), typeof(float)});
        private static readonly FieldInfo vector2FieldX = AccessTools.Field(typeof(Vector2), "x");
        private static readonly FieldInfo vector2FieldY = AccessTools.Field(typeof(Vector2), "y");

        // VariedBodySizes.RenderPatches
        private static readonly MethodBase getScalar = AccessTools.Method("VariedBodySizes.RenderPatches:GetScalarForPawn");

        [HarmonyPatch]
        //[HarmonyDebug]
        private static class GraphicMeshSet_GetHumanlikeSetForPawnPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return getHeadSet;
                yield return getBodySet;
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
            {
                // store which method we are patching currently
                var isHead = original == getHeadSet;
                
                // The stack before every ret will just be the mesh for the pawn
                // We can drop in before each ret, clear the stack and do our things.
                // We'll need a local to convert the vector to x,y though
                var localVec = generator.DeclareLocal(typeof(Vector2));
                var editor = new CodeMatcher(instructions);
                
                // First change: add our local to the start of the function
                editor.Start().Insert(
                    // Pawn
                    new CodeInstruction(OpCodes.Ldarg_0),
                    // Add a bool to the stack - whether this is head or body
                    new CodeInstruction(OpCodes.Ldc_I4, isHead ? 1 : 0),
                    // Pawn scalar (Vec2) to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // Store to local
                    new CodeInstruction(OpCodes.Stloc, localVec.LocalIndex)
                );
                
                // Second change: MeshPool.humanlikeHead/BodySet -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidth)
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldsfld, isHead ? headSetField : bodySetField)
                ).Repeat(match =>
                    match.SetAndAdvance(
                        OpCodes.Ldc_R4, isHead ? MeshPool.HumanlikeHeadAverageWidth : MeshPool.HumanlikeBodyWidth
                        ).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, getMeshSet1D)
                    ));
                
                // Third change: MeshPool.GetMeshSetForWidth(float) -> MeshPool.GetMeshSetForWidth(local.x * float, local.y * float)
                editor.Start().MatchStartForward(
                        new CodeMatch(OpCodes.Call, getMeshSet1D)
                    ).Repeat(match => 
                        match.SetAndAdvance(
                                // Grab our local multiplier
                                OpCodes.Ldloc, localVec.LocalIndex
                            ).InsertAndAdvance(
                            // We have a float on the stack already, multiply the two
                            new CodeInstruction(OpCodes.Call, floatTimesVector2),
                            // Pop back to the local
                            new CodeInstruction(OpCodes.Stloc, localVec.LocalIndex),
                            new CodeInstruction(OpCodes.Ldloc, localVec.LocalIndex),
                            // Push X and Y to stack
                            new CodeInstruction(OpCodes.Ldfld, vector2FieldX),
                            new CodeInstruction(OpCodes.Ldloc, localVec.LocalIndex),
                            new CodeInstruction(OpCodes.Ldfld, vector2FieldY),
                            // Call 2d mesh scalar
                            new CodeInstruction(OpCodes.Call, getMeshSet2D)
                        ));
                
                // Done
                return editor.InstructionEnumeration();
            }
        }

        [HarmonyPatch]
        //[HarmonyDebug]
        private static class GraphicMeshSet_GetHairBeardSetForPawnPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return getHairSet;
                yield return getBeardSet;
            }
            
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
                ILGenerator generator, MethodBase original)
            {
                // var vector *= getScalar(pawn, true)
                return new CodeMatcher(instructions).Start().MatchStartForward(
                    new CodeMatch(OpCodes.Stloc_0)
                ).Advance(1).InsertAndAdvance(
                        // Pawn to stack
                        new CodeInstruction(OpCodes.Ldarg_0),
                        // Add a bool to the stack - this is a head scalar
                        new CodeInstruction(OpCodes.Ldc_I4, 1),
                        // Pawn scalar (Vec2) to stack, consumes current stack
                        new CodeInstruction(OpCodes.Call, getScalar),
                        // Grab our local and multiply by the pawn scalar
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, vector2TimesVector2),
                        // Store
                        new CodeInstruction(OpCodes.Stloc_0)
                    ).InstructionEnumeration();;
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
        
        [HarmonyPatch]
        //[HarmonyDebug]
        private static class PawnRenderer_BaseHeadOffsetAt_Patch
        {
            private static MethodBase TargetMethod()
            {
                return headOffsetAt;
            }
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // Find where it roots the age/size factor
                return new CodeMatcher(instructions).Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, ageBodyFactor)
                ).Advance(1).InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, renderPawn),
                    // Add a bool to the stack - this is a head scalar
                    new CodeInstruction(OpCodes.Ldc_I4, 1),
                    // Pawn scalar (Vec2) to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // Grab our local and multiply by the pawn scalar
                    new CodeInstruction(OpCodes.Ldfld, vector2FieldY),
                    new CodeInstruction(OpCodes.Mul)
                ).InstructionEnumeration();;
            }
        }

        [HarmonyPatch]
        private static class PawnRenderer_DrawBodyGenesPatch
        {
            private static MethodBase TargetMethod()
            {
                return drawBodyGenes;
            }
            
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // Find where it roots the age/size factor
                return new CodeMatcher(instructions).Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, bodyScale)
                ).Advance(1).InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, renderPawn),
                    // Add a bool to the stack - this is a body scalar
                    new CodeInstruction(OpCodes.Ldc_I4, 0),
                    // Pawn scalar (Vec2) to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // multiply by the pawn scalar
                    new CodeInstruction(OpCodes.Call, vector2TimesVector2)
                ).InstructionEnumeration();
            }
        }
        
        [HarmonyPatch]
        //[HarmonyDebug]
        private static class PawnRenderer_DrawExtraEyeGraphicPatch
        {
            private static FieldInfo eyeOverlayPawnRendererField;

            // Verse.PawnRenderer+<>c__DisplayClass54_0:<DrawHeadHair>g__DrawExtraEyeGraphic|6
            // Grab the "this" so we can move upwards later

            private static readonly MethodBase drawEyeOverlay =
                AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(PawnRenderer), type => {
                    return AccessTools.FirstMethod(type, info =>
                    {
                        if (!info.Name.Contains("DrawExtraEyeGraphic")) return false;
                        eyeOverlayPawnRendererField = AccessTools.GetDeclaredFields(type)
                            .First(field => field.FieldType == typeof(PawnRenderer));
                        return true;
                    });
                });

            private static bool Prepare()
            {
                return drawEyeOverlay != null;
            }

            private static MethodBase TargetMethod()
            {
                return drawEyeOverlay;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // Find where it roots the age/size factor
                return new CodeMatcher(instructions).Start().InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, eyeOverlayPawnRendererField),
                    new CodeInstruction(OpCodes.Ldfld, renderPawn),
                    // Add a bool to the stack - this is a head scalar
                    new CodeInstruction(OpCodes.Ldc_I4, 1),
                    // Pawn scalar (Vec2) to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // 1D
                    new CodeInstruction(OpCodes.Ldfld, vector2FieldX),
                    // Scale factor to stack
                    new CodeInstruction(OpCodes.Ldarg_2),
                    // multiply by the pawn scalar
                    new CodeInstruction(OpCodes.Mul),
                    // Store back as arg
                    new CodeInstruction(OpCodes.Starg, 2)
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