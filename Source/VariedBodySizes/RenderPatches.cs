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
using FieldInfo = System.Reflection.FieldInfo;

namespace VariedBodySizes;

// Note to self: args(oldest, newer, newest)
public static class RenderPatches
{
    // Commented until their compat gets in order
    private static readonly bool hasVef = false;//ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core");

    private static bool NotNull(params object[] input)
    {
        if (input.All(o => o is not null)) return true;
        Main.LogMessage("VariedBodySizes: Signature match not found", true);
        foreach (var obj in input)
        {
            if (obj is MemberInfo memberObj)
            {
                Main.LogMessage($"\tValid entry:{memberObj}", true);
            }
        }
        return false;
    }

    private static IEnumerable<MethodBase> YieldAll(params MethodBase[] input)
    {
        return input;
    }

    // CodeMatcher will throw errors if we try to take actions in an invalid state (i.e. no match)
    private static void OnSuccess(CodeMatcher match, Action<CodeMatcher> action)
    {
        if (match.IsInvalid)
        {
            Main.LogMessage("Transpiler did not find target", true);
            return;
        }
        action.Invoke(match);
    }

    private static float GetScalarForPawn(Pawn pawn)
    {
        return Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f;
    }

    private static readonly MethodBase getHumanlikeBodyWidthFunc =
        AccessTools.Method(typeof(HumanlikeMeshPoolUtility), "HumanlikeBodyWidthForPawn");

    private static readonly FieldInfo pawnRendererPawn = AccessTools.Field("Verse.PawnRenderer:pawn");

    private static readonly MethodBase floatTimesVector2 =
        AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(float), typeof(Vector2)});

    private static readonly MethodBase getScalar =
        AccessTools.Method(typeof(RenderPatches),"GetScalarForPawn");

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

            private static bool ProcessField<TKey>(Dictionary<TKey, Vector2> backingDict, TKey key, Graphic graphic, float pawnScale, out Graphic scaledGraphic)
            {
                scaledGraphic = default;
                if (key is null) return false;
                if (graphic is null) return false;
                backingDict.TryAdd(key, graphic.drawSize);

                var originalSize = backingDict[key];
                var scaledSize = originalSize * pawnScale;

                // Nothing to do
                if (originalSize == scaledSize) return false;
                if (scaledSize == graphic.drawSize) return false;

                // Graphic was changed somehow
                if (originalSize != graphic.drawSize)
                {
                    originalSize = graphic.drawSize;
                    scaledSize = originalSize * pawnScale;
                    backingDict[key] = originalSize;
                }

                scaledGraphic = graphic.GetCopy(scaledSize, null);
                return true;
            }

            [UsedImplicitly]
            private static bool Prepare() => NotNull(resolveAllGraphics);

            [UsedImplicitly]
            private static MethodBase TargetMethod() => resolveAllGraphics;

            [UsedImplicitly]
            private static void Postfix(PawnGraphicSet __instance, Pawn ___pawn)
            {
                var pawnDrawSize = GetScalarForPawn(___pawn);
                
                // Build dict entry if not already present
                if (!originalSizes.ContainsKey(___pawn))
                {
                    originalSizes.Add(___pawn, new Dictionary<FieldInfo, Vector2>());
                    originalGeneSizes.Add(___pawn, new Dictionary<Gene, Vector2>());
                    originalGearSizes.Add(___pawn, new Dictionary<Apparel, Vector2>());
                }
                
                // Regular graphics incl naked body for animals
                foreach (var field in graphicFields)
                {
                    if (!ProcessField(originalSizes[___pawn], field, field.GetValue(__instance) as Graphic, pawnDrawSize,
                            out var scaledGraphic)) continue;
                    // Resize and continue
                    field.SetValue(__instance, scaledGraphic);
                }

                // Gene graphics like eye overlays
                for (var i = 0; i < (__instance.geneGraphics?.Count ?? 0); i++)
                {
                    var geneGraphic = __instance.geneGraphics![i];

                    // If it doesn't match we won't have a rotting graphic anyway
                    if (!ProcessField(originalGeneSizes[___pawn], geneGraphic.sourceGene, geneGraphic.graphic, pawnDrawSize,
                            out var scaledGraphic)) continue;
                    
                    geneGraphic.graphic = scaledGraphic;
                    
                    if (ProcessField(originalGeneSizes[___pawn], geneGraphic.sourceGene, geneGraphic.rottingGraphic, pawnDrawSize,
                            out var scaledRottingGraphic))
                        geneGraphic.rottingGraphic = scaledRottingGraphic;

                    // Pop back and continue
                    __instance.geneGraphics[i] = new GeneGraphicRecord(geneGraphic.graphic,
                        geneGraphic.rottingGraphic, geneGraphic.sourceGene);
                }
                
                // Apparel graphics 
                for (var i = 0; i < (__instance.apparelGraphics?.Count ?? 0); i++)
                {
                    var gearGraphic = __instance.apparelGraphics![i];
                    
                    if (!ProcessField(originalGearSizes[___pawn], gearGraphic.sourceApparel, gearGraphic.graphic, pawnDrawSize,
                            out var scaledGraphic)) continue;
                    
                    // Pop back and continue
                    __instance.apparelGraphics[i] =
                        new ApparelGraphicRecord(scaledGraphic, gearGraphic.sourceApparel);
                }
                
                // Done
            }
        }
    }
    
    // Base game
    private static class PatchesNoVef
    {

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

                // First change: MeshPool.GetMeshSetForWidth(pawn.ageTracker.CurLifeStage.bodyWidth.Value) -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld),
                    new CodeMatch(OpCodes.Callvirt),
                    new CodeMatch(OpCodes.Ldflda),
                    new CodeMatch(OpCodes.Call),
                    new CodeMatch(OpCodes.Call, getMeshSet1D)
                );

                // If success
                OnSuccess(editor, match =>
                    match.RemoveInstructions(4).InsertAndAdvance(
                        // We already have our pawn on the stack so we just replace our match with the function call
                        new CodeInstruction(OpCodes.Call, getHumanlikeBodyWidthFunc)
                    )
                );

                // Second change: MeshPool.humanlikeHead/BodySet -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldsfld, isHead ? headSetField : bodySetField)
                );

                OnSuccess(editor, match => 
                    match.SetAndAdvance(
                        OpCodes.Ldarg_S, 0 // pawn to stack
                    ).InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, getHumanlikeBodyWidthFunc), // Pop pawn, push width
                        new CodeInstruction(OpCodes.Call, getMeshSet1D) // Pop width, push mesh
                    )
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
                var editor = new CodeMatcher(instructions);
                // var vector *= GetScalarForPawn(pawn)
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Stloc_0)
                );
                OnSuccess(editor, match => match.Advance(1).InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    // Pawn scalar to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // Grab our local and multiply by the pawn scalar
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Call, floatTimesVector2),
                    // Store
                    new CodeInstruction(OpCodes.Stloc_0)
                ));
                return editor.InstructionEnumeration();
            }
        }
        
        [HarmonyPatch]
        //[HarmonyDebug]
        [UsedImplicitly]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class PawnRenderer_GetBodyOverlayMeshSetPatch
        {
            private static readonly MethodBase getBodyOverlayMesh = AccessTools.Method(typeof(PawnRenderer), "GetBodyOverlayMeshSet");
            private static readonly TimedCache<GraphicMeshSet> overlayCache = new(360);
            
            private static GraphicMeshSet TranslateForPawn(GraphicMeshSet baseMesh, Pawn pawn)
            {
                // North[2] is positive on both x and y axis. Defaults would be 0.65,0,0.65 times 2 for the default 1.3f
                var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2 * GetScalarForPawn(pawn);
                return MeshPool.GetMeshSetForWidth(baseVector.x, baseVector.z);
            }
            
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
                var editor = new CodeMatcher(instructions);
                // Find where it roots the age/size factor
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, ageBodyFactor)
                );
                OnSuccess(editor, match => match.Advance(1).InsertAndAdvance(
                    // Pawn to stack
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, pawnRendererPawn),
                    // Pawn scalar to stack, consumes current stack
                    new CodeInstruction(OpCodes.Call, getScalar),
                    // Multiply ageBodyFactor by our scalar
                    new CodeInstruction(OpCodes.Mul)
                ));
                return editor.InstructionEnumeration();
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
                var editor = new CodeMatcher(instructions);
                // Find where it checks body scale
                editor.Start().MatchStartForward(
                    new CodeMatch(OpCodes.Ldfld, bodyScale)
                );
                OnSuccess(editor, match => match.Advance(1).InsertAndAdvance(
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
                ));
                return editor.InstructionEnumeration();
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
                // Modify the 2nd arg - scale (float) - right at the start of the function
                // Since we use repeat() everywhere we don't have to care about checking for validity.. probably
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
                    // First two instances of wound offset. Can't just modify vector3_1 since it's used elsewhere.
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