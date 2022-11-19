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
using UnityEngine.SocialPlatforms;
using Verse;
using CodeInstruction = HarmonyLib.CodeInstruction;
using FieldInfo = System.Reflection.FieldInfo;

namespace VariedBodySizes;

// Note to self: args(oldest, newer, newest)
public static class RenderPatches
{
    // Commented until their compat gets in order
    private static readonly bool hasVef = false;//ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core");

    private static readonly MethodBase getHumanlikeBodyWidthFunc =
        AccessTools.Method(typeof(HumanlikeMeshPoolUtility), "HumanlikeBodyWidthForPawn");

    private static readonly FieldInfo pawnRendererPawn = AccessTools.Field("Verse.PawnRenderer:pawn");

    private static readonly MethodBase floatTimesVector2 =
        AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(float), typeof(Vector2)});
    private static readonly MethodBase vector2TimesFloat =
        AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(Vector2), typeof(float)});

    private static readonly MethodBase getScalar =
        AccessTools.Method(typeof(RenderPatches),"GetScalarForPawn");

    private static readonly TimedCache<GraphicMeshSet> overlayCache = new(360);

    private static bool NotNull(params object[] input)
    {
        if (input.All(o => o is not null)) return true;
        Log.Warning("VariedBodySizes: Signature match not found");
        foreach (var obj in input)
        {
            if (obj is MemberInfo memberObj)
            {
                Log.Warning($"\tValid entry:{memberObj.Name}");
            }
        }
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

    private static GraphicMeshSet TranslateForPawn(GraphicMeshSet baseMesh, Pawn pawn)
    {
        // North[2] is positive on both x and y axis. Defaults would be 0.65,0,0.65 times 2 for the default 1.3f
        var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2 * GetScalarForPawn(pawn);
        return MeshPool.GetMeshSetForWidth(baseVector.x, baseVector.z);
    }

    // Unconditional
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly]
    [HarmonyBefore("OskarPotocki.VFECore")]
    private static class PatchesUnconditional
    {
        [HarmonyPatch]
        [UsedImplicitly]
        private static class HumanlikeMeshPoolUtility_HumanlikeBodyWidthForPawnPatch
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

        [HarmonyPatch]
        //[HarmonyDebug]
        [UsedImplicitly]
        private static class PawnGraphicSet_ResolveAllGraphicsPatch
        {
            
            private static readonly Dictionary<Pawn, Dictionary<FieldInfo, Vector2>> originalSizes = new();
            private static readonly Dictionary<Pawn, Dictionary<Gene, Vector2>> originalGeneSizes = new();
            private static readonly Dictionary<Pawn, Dictionary<Apparel, Vector2>> originalGearSizes = new();
            private static readonly IEnumerable<FieldInfo> graphicFields = AccessTools.GetDeclaredFields(typeof(PawnGraphicSet))
                .Where(f => f.FieldType == typeof(Graphic));
            private static readonly MethodBase resolveAllGraphics = AccessTools.Method(typeof(PawnGraphicSet), "ResolveAllGraphics");

            [UsedImplicitly]
            private static bool Prepare() => NotNull(resolveAllGraphics);

            [UsedImplicitly]
            private static MethodBase TargetMethod() => resolveAllGraphics;

            [UsedImplicitly]
            private static void Postfix(PawnGraphicSet __instance, Pawn ___pawn)
            {
                Log.Warning($"Resolving graphics for {___pawn}");
                var pawnDrawSize = GetScalarForPawn(___pawn);
                // Build dict entry if not already present
                if (!originalSizes.ContainsKey(___pawn))
                {
                    originalSizes.Add(___pawn, new Dictionary<FieldInfo, Vector2>());
                    originalGeneSizes.Add(___pawn, new Dictionary<Gene, Vector2>());
                    originalGearSizes.Add(___pawn, new Dictionary<Apparel, Vector2>());
                }

                var pawnSizeList = originalSizes[___pawn];
                var pawnGeneSizeList = originalGeneSizes[___pawn];
                var pawnGearSizeList = originalGearSizes[___pawn];
                foreach (var field in graphicFields)
                {
                    if (field.GetValue(__instance) is not Graphic fieldGraphic) continue;


                    pawnSizeList.TryAdd(field, fieldGraphic.drawSize);

                    var originalSize = pawnSizeList[field];
                    var scaledSize = originalSize * pawnDrawSize;

                    // Nothing to do
                    if (originalSize == scaledSize) continue;
                    if (scaledSize == fieldGraphic.drawSize) continue;

                    // Graphic was changed somehow
                    if (originalSize != fieldGraphic.drawSize)
                    {
                        originalSize = fieldGraphic.drawSize;
                        scaledSize = originalSize * pawnDrawSize;
                        pawnSizeList[field] = originalSize;
                    }

                    // Resize and continue
                    field.SetValue(__instance, fieldGraphic.GetCopy(scaledSize, null));
                }

                for (var i = 0; i < (__instance.geneGraphics?.Count ?? 0); i++)
                {
                    var geneGraphic = __instance.geneGraphics![i];
                    if (geneGraphic.sourceGene is null) continue;
                    if (geneGraphic.graphic is null) continue;
                    pawnGeneSizeList.TryAdd(geneGraphic.sourceGene, geneGraphic.graphic.drawSize);
                    var originalSize = pawnGeneSizeList[geneGraphic.sourceGene];
                    var scaledSize = originalSize * pawnDrawSize;

                    // Nothing to do
                    if (originalSize == scaledSize) continue;
                    if (scaledSize == geneGraphic.graphic.drawSize) continue;

                    // Graphic was changed somehow
                    if (originalSize != geneGraphic.graphic.drawSize)
                    {
                        originalSize = geneGraphic.graphic.drawSize;
                        scaledSize = originalSize * pawnDrawSize;
                        pawnGeneSizeList[geneGraphic.sourceGene] = originalSize;
                    }

                    geneGraphic.graphic = geneGraphic.graphic.GetCopy(scaledSize, null);

                    if (geneGraphic.rottingGraphic is not null)
                    {
                        geneGraphic.rottingGraphic = geneGraphic.rottingGraphic.GetCopy(scaledSize, null);
                    }

                    // Pop back and continue
                    __instance.geneGraphics[i] = new GeneGraphicRecord(geneGraphic.graphic,
                        geneGraphic.rottingGraphic, geneGraphic.sourceGene);
                }
                for (var i = 0; i < (__instance.apparelGraphics?.Count ?? 0); i++)
                {
                    var gearGraphic = __instance.apparelGraphics![i];
                    if (gearGraphic.sourceApparel is null) continue;
                    if (gearGraphic.graphic is null) continue;
                    pawnGearSizeList.TryAdd(gearGraphic.sourceApparel, gearGraphic.graphic.drawSize);
                    var originalSize = pawnGearSizeList[gearGraphic.sourceApparel];
                    var scaledSize = originalSize * pawnDrawSize;

                    // Nothing to do
                    if (originalSize == scaledSize) continue;
                    if (scaledSize == gearGraphic.graphic.drawSize) continue;

                    // Graphic was changed somehow
                    if (originalSize != gearGraphic.graphic.drawSize)
                    {
                        originalSize = gearGraphic.graphic.drawSize;
                        scaledSize = originalSize * pawnDrawSize;
                        pawnGearSizeList[gearGraphic.sourceApparel] = originalSize;
                    }

                    gearGraphic.graphic = gearGraphic.graphic.GetCopy(scaledSize, null);

                    // Pop back and continue
                    __instance.apparelGraphics[i] =
                        new ApparelGraphicRecord(gearGraphic.graphic, gearGraphic.sourceApparel);
                }
            }
        }
    }
    
    // Base game
    private static class PatchesNoVef
    {

        [HarmonyPatch]
        [HarmonyDebug]
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

                // First change: MeshPool.GetMeshSetForWidth(pawn.ageTracker.CurLifeStage.bodyWidth.Value) -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Ldflda),
                    new CodeMatch(OpCodes.Call),
                    new CodeMatch(OpCodes.Call, getMeshSet1D)
                ).RemoveInstructions(4).InsertAndAdvance(
                    // We already have our pawn on the stack so we just replace our match with the function call
                    new CodeInstruction(OpCodes.Call, getHumanlikeBodyWidthFunc)
                );

                // Second change: MeshPool.humanlikeHead/BodySet -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldsfld, isHead ? headSetField : bodySetField)
                ).SetAndAdvance(
                    OpCodes.Ldarg_S, 0 // pawn to stack
                ).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Call, getHumanlikeBodyWidthFunc), // Pop pawn, push width
                    new CodeInstruction(OpCodes.Call, getMeshSet1D) // Pop width, push mesh
                );
                
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
                if (!overlayCache.TryGet(pawn, out var returnedMesh))
                    return overlayCache.SetAndReturn(pawn, TranslateForPawn(baseMesh, pawn));
                return returnedMesh;
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
        private static class PawnRenderer_BaseHeadOffsetAtPatch
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
        //[HarmonyDebug]
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
            [UsedImplicitly]
            private static Vector3 ModifyVectorForPawn(Vector3 vec, Pawn pawn)
            {
                var scalar = GetScalarForPawn(pawn);
                vec.x *= scalar;
                vec.z /= scalar;
                return vec;
            }

            private static readonly MethodBase modifyVec =
                AccessTools.Method(typeof(PawnRenderer_DrawExtraEyeGraphicPatch), "ModifyVectorForPawn");
            // Verse.PawnRenderer+<>c__DisplayClass54_0:<DrawHeadHair>g__DrawExtraEyeGraphic|6
            private static readonly MethodBase drawEyeOverlay =
                AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(PawnRenderer),
                    type => AccessTools.FirstMethod(type, info => info.Name.Contains("DrawExtraEyeGraphic")));

            // Grab the "this" so we can move upwards later
            private static readonly FieldInfo eyeOverlayPawnRendererField = AccessTools
                .GetDeclaredFields(drawEyeOverlay?.DeclaringType)
                .First(field => field.FieldType == typeof(PawnRenderer));

            private static readonly FieldInfo
                woundOffset = AccessTools.Field(typeof(BodyTypeDef.WoundAnchor), "offset");

            [UsedImplicitly]
            private static bool Prepare() => !hasVef && NotNull(drawEyeOverlay, eyeOverlayPawnRendererField, pawnRendererPawn, woundOffset);

            [UsedImplicitly]
            private static MethodBase TargetMethod() => drawEyeOverlay;

            [UsedImplicitly]
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var woundOffsetCount = 0;
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
                ).Start().MatchStartForward(
                    new CodeMatch(i => i.IsLdloc()),
                    new CodeMatch(OpCodes.Ldfld, woundOffset),
                    new CodeMatch(_ => woundOffsetCount++ < 3)
                ).Repeat(match => match.Advance(2).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, eyeOverlayPawnRendererField),
                    new CodeInstruction(OpCodes.Ldfld, pawnRendererPawn),
                    new CodeInstruction(OpCodes.Call, modifyVec)
                )).Start().MatchStartForward(
                    // vector 3_2, vector 3_3
                    new CodeMatch(i => {
                        if (!i.IsLdloc()) return false;
                        if (i.operand is not LocalBuilder candidateLocal) return false;
                        if (candidateLocal.LocalType != typeof(Vector3)) return false;
                        return candidateLocal.LocalIndex >= 3;
                    })
                ).Repeat(match => match.Advance(1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, eyeOverlayPawnRendererField),
                    new CodeInstruction(OpCodes.Ldfld, pawnRendererPawn),
                    new CodeInstruction(OpCodes.Call, modifyVec)
                )).InstructionEnumeration();
            }
        }
    }
}