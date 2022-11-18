using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using CodeInstruction = HarmonyLib.CodeInstruction;

namespace VariedBodySizes;

public static class RenderPatches
{
    private static readonly bool hasVef = ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core");

    private static readonly MethodBase getHumanlikeBodyWidthFunc =
        AccessTools.Method(typeof(HumanlikeMeshPoolUtility), "HumanlikeBodyWidthForPawn");

    private static readonly TimedCache<GraphicMeshSet> overlayCache = new(360);

    private static bool NotNull(params object[] input)
    {
        if (input.All(o => o is not null)) return true;
        Log.Warning("VariedBodySizes: Signature match not found");
        return false;
    }

    private static IEnumerable<MethodBase> YieldAll(params MethodBase[] input)
    {
        return input;
    }

    private static float GetScalarForPawn(Pawn pawn)
    {
        return Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f;
    }

    // Unconditional
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly]
    [HarmonyPatch]
    [HarmonyBefore("OskarPotocki.VFECore")]
    private static class PatchesUnconditional
    {
        [UsedImplicitly]
        private static bool Prepare() => NotNull(getHumanlikeBodyWidthFunc);

        [UsedImplicitly]
        private static MethodBase TargetMethod() => getHumanlikeBodyWidthFunc;

        [UsedImplicitly]
        private static void Postfix(Pawn pawn, ref float __result)
        {
            __result *= GetScalarForPawn(pawn);
        }
    }
    
    // Base game
    private static class PatchesNoVef
    {
        // PawnRenderer.Pawn field
        private static readonly FieldInfo pawnRendererPawn = AccessTools.Field("Verse.PawnRenderer:pawn");

        private static readonly MethodBase floatTimesVector2 =
            AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(float), typeof(Vector2)});

        private static readonly MethodBase getScalar =
            AccessTools.Method(typeof(RenderPatches),"GetScalarForPawn");

        [HarmonyPatch]
        //[HarmonyDebug]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class GraphicMeshSet_GetHumanlikeSetForPawnPatch
        {
            private static readonly MethodBase getHeadSet =
                AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHeadSetForPawn");

            private static readonly MethodBase getBodySet =
                AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBodySetForPawn");

            private static readonly FieldInfo headSetField = AccessTools.Field("Verse.MeshPool:humanlikeHeadSet");
            private static readonly FieldInfo bodySetField = AccessTools.Field("Verse.MeshPool:humanlikeBodySet");

            private static readonly MethodBase getMeshSet1D =
                AccessTools.Method("Verse.MeshPool:GetMeshSetForWidth", new[] {typeof(float)});

            [UsedImplicitly]
            private static bool Prepare() => !hasVef && NotNull(getHeadSet, getBodySet, headSetField, bodySetField, getMeshSet1D);

            [UsedImplicitly]
            private static IEnumerable<MethodBase> TargetMethods() => YieldAll(getHeadSet, getBodySet);

            [UsedImplicitly]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
                ILGenerator generator, MethodBase original)
            {
                // store which method we are patching currently
                var isHead = original == getHeadSet;
                var editor = new CodeMatcher(instructions);

                // Second change: MeshPool.humanlikeHead/BodySet -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldsfld, isHead ? headSetField : bodySetField)
                ).Repeat(match =>
                    match.SetAndAdvance(
                        OpCodes.Ldarg_S, 0 // pawn to stack
                    ).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, getHumanlikeBodyWidthFunc), // Pop pawn, push width
                        new CodeInstruction(OpCodes.Call, getMeshSet1D) // Pop width, push mesh
                    ));

                // Third change: MeshPool.GetMeshSetForWidth(pawn.ageTracker.CurLifeStage.bodyWidth.Value) -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
                editor.Start().MatchStartForward(
                    new CodeMatch(CodeInstruction.LoadField(typeof(Pawn), "ageTracker")),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(CodeInstruction.LoadField(typeof(LifeStageDef), "bodyWidth")),
                    new CodeMatch(CodeInstruction.Call(typeof(float?), "get_Value"))
                ).Repeat(match =>
                    match.RemoveInstructions(4).InsertAndAdvance(
                        // We already have our pawn on the stack so we just replace our match with the function call
                        new CodeInstruction(OpCodes.Call, getHumanlikeBodyWidthFunc)
                    ));

                // Done
                return editor.InstructionEnumeration();
            }
        }

        [HarmonyPatch]
        //[HarmonyDebug]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class GraphicMeshSet_GetHairBeardSetForPawnPatch
        {
            private static readonly MethodBase getHairSet =
                AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHairSetForPawn");

            private static readonly MethodBase getBeardSet =
                AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBeardSetForPawn");

            [UsedImplicitly]
            private static bool Prepare() => !hasVef && NotNull(getHairSet, getBeardSet, floatTimesVector2);

            [UsedImplicitly]
            private static IEnumerable<MethodBase> TargetMethods() => YieldAll(getHairSet, getBeardSet);

            [UsedImplicitly]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
                ILGenerator generator, MethodBase original)
            {
                // var vector *= GetScalarForPawn(pawn)
                return new CodeMatcher(instructions).Start().MatchStartForward(
                    new CodeMatch(OpCodes.Stloc_0)
                ).Advance(1).InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    // Pawn scalar to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // Grab our local and multiply by the pawn scalar
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Call, floatTimesVector2),
                    // Store
                    new CodeInstruction(OpCodes.Stloc_0)
                ).InstructionEnumeration();
            }
        }
        
        [HarmonyPatch]
        //[HarmonyDebug]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class PawnRenderer_GetBodyOverlayMeshSetPatch
        {
            private static readonly MethodBase getBodyOverlayMesh = AccessTools.Method(typeof(PawnRenderer), "GetBodyOverlayMeshSet");
            
            [UsedImplicitly]
            private static bool Prepare() => !hasVef && NotNull(getBodyOverlayMesh);

            [UsedImplicitly]
            private static MethodBase TargetMethod() => getBodyOverlayMesh;
            
            private static GraphicMeshSet GetBodyOverlayMeshForPawn(GraphicMeshSet baseMesh, Pawn pawn)
            {
                var returnedMesh = overlayCache.Get(pawn);

                if (returnedMesh != null)
                {
                    return returnedMesh;
                }

                // North[2] is positive on both x and y axis. Defaults would be 0.65,0,0.65 times 2 for the default 1.3f
                var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2 * GetScalarForPawn(pawn);
                var scaledMesh = MeshPool.GetMeshSetForWidth(baseVector.x, baseVector.z);
                overlayCache.Set(pawn, scaledMesh);
                return scaledMesh;
            }

            [UsedImplicitly]
            private static void Postfix(ref GraphicMeshSet __result, Pawn ___pawn)
            {
                __result = GetBodyOverlayMeshForPawn(__result, ___pawn);
            }
        }

        [HarmonyPatch]
        //[HarmonyDebug]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class PawnRenderer_BaseHeadOffsetAt_Patch
        {
            private static readonly MethodBase headOffsetAt = AccessTools.Method("Verse.PawnRenderer:BaseHeadOffsetAt");
            private static readonly FieldInfo ageBodyFactor = AccessTools.Field("RimWorld.LifeStageDef:bodySizeFactor");

            [UsedImplicitly]
            private static bool Prepare() => !hasVef && NotNull(headOffsetAt, ageBodyFactor, pawnRendererPawn);

            [UsedImplicitly]
            private static MethodBase TargetMethod() => headOffsetAt;

            [UsedImplicitly]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // Find where it roots the age/size factor
                return new CodeMatcher(instructions).Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, ageBodyFactor)
                ).Advance(1).InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, pawnRendererPawn),
                    // Pawn scalar to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // Multiply ageBodyFactor by our scalar
                    new CodeInstruction(OpCodes.Mul)
                ).InstructionEnumeration();
            }
        }

        [HarmonyPatch]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class PawnRenderer_DrawBodyGenesPatch
        {
            private static readonly MethodBase drawBodyGenes = AccessTools.Method("Verse.PawnRenderer:DrawBodyGenes");
            private static readonly FieldInfo bodyScale = AccessTools.Field("RimWorld.BodyTypeDef:bodyGraphicScale");

            [UsedImplicitly]
            private static bool Prepare() => !hasVef && NotNull(drawBodyGenes, bodyScale, pawnRendererPawn, floatTimesVector2);

            [UsedImplicitly]
            private static MethodBase TargetMethod() => drawBodyGenes;

            [UsedImplicitly]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // Find where it checks body scale
                return new CodeMatcher(instructions).Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, bodyScale)
                ).Advance(1).InsertAndAdvance(
                    // Pop body scale to local
                    new CodeInstruction(OpCodes.Stloc_0),
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, pawnRendererPawn),
                    // Pawn scalar to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // Push body scale from local
                    new CodeInstruction(OpCodes.Ldloc_0),
                    // multiply by the pawn scalar
                    new CodeInstruction(OpCodes.Call, floatTimesVector2)
                ).InstructionEnumeration();
            }
        }

        [HarmonyPatch]
        //[HarmonyDebug]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class PawnRenderer_DrawExtraEyeGraphicPatch
        {

            // Verse.PawnRenderer+<>c__DisplayClass54_0:<DrawHeadHair>g__DrawExtraEyeGraphic|6
            private static readonly MethodBase drawEyeOverlay =
                AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(PawnRenderer),
                    type => AccessTools.FirstMethod(type, info => info.Name.Contains("DrawExtraEyeGraphic")));

            // Grab the "this" so we can move upwards later
            private static readonly FieldInfo eyeOverlayPawnRendererField = AccessTools
                .GetDeclaredFields(drawEyeOverlay?.DeclaringType)
                .First(field => field.FieldType == typeof(PawnRenderer));

            [UsedImplicitly]
            private static bool Prepare() => !hasVef && NotNull(drawEyeOverlay, eyeOverlayPawnRendererField, pawnRendererPawn);

            [UsedImplicitly]
            private static MethodBase TargetMethod() => drawEyeOverlay;

            [UsedImplicitly]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // Modify the 2nd arg, scale (float) right at the start of the function
                return new CodeMatcher(instructions).Start().InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, eyeOverlayPawnRendererField),
                    new CodeInstruction(OpCodes.Ldfld, pawnRendererPawn),
                    // Pawn scalar to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
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
}