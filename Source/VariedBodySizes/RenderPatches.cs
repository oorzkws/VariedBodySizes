using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
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
    private static readonly bool hasVEF = ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core");
    
    private static readonly TimedCache<GraphicMeshSet> headCache = new(360);
    private static readonly TimedCache<GraphicMeshSet> bodyCache = new(360);
    private static readonly TimedCache<GraphicMeshSet> hairCache = new(360);
    private static readonly TimedCache<GraphicMeshSet> beardCache = new(360);
    private static readonly TimedCache<GraphicMeshSet> overlayCache = new(360);


    private static GraphicMeshSet GetHeadBodyMeshForPawn(float scaleFactor, Pawn pawn, bool isHead)
    {
        var targetCache = isHead ? headCache : bodyCache;

        if (targetCache.TryGet(pawn, out var returnedMesh))
            return returnedMesh;
        
        var scaledFactor = scaleFactor * (Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f);
        var scaledMesh = MeshPool.GetMeshSetForWidth(scaledFactor);
        targetCache.Set(pawn, scaledMesh);
        return scaledMesh;
    }

    private static GraphicMeshSet GetHairBeardMeshForPawn(Vector2 scalar, Pawn pawn, bool isHair)
    {
        var targetCache = isHair ? hairCache : beardCache;

        if (targetCache.TryGet(pawn, out var returnedMesh))
            return returnedMesh;
        
        var scaledFactor = scalar * (Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f);
        var scaledMesh = MeshPool.GetMeshSetForWidth(scaledFactor.x, scaledFactor.y);
        targetCache.Set(pawn, scaledMesh);
        return scaledMesh;
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
            var bodyFactor = Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f;
            var scaledMesh = MeshPool.GetMeshSetForWidth(baseVector.x * bodyFactor, baseVector.y * bodyFactor);
            overlayCache.Set(pawn, scaledMesh);
            return scaledMesh;
        }
        
    }
    
    private static Vector2 returnModifiedDrawHeight(Vector2 vec, Pawn pawn)
    {
        return vec * Mathf.Sqrt(Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f);
    }
    
    // Unconditional
    [HarmonyBefore("OskarPotocki.VFECore")]
    private static class PatchesUnconditional{
        
        [HarmonyPatch]
        //[HarmonyDebug]
        private static class GraphicMeshSet_GetHumanlikeSetForPawnPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHeadSetForPawn");
                yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBodySetForPawn");
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
            {
                // Outcome is essentially:
                // return MeshPool.GetMeshSetForWidth(!ModsConfig.BiotechActive ? 1.5f : pawn.ageTracker.CurLifeStage.bodyWidth ?? 1.5f);
                
                var localNullableFloat = generator.DeclareLocal(typeof(float?));
                // These are required for jumps
                var labelNum0028 = generator.DefineLabel();
                var labelNum0031 = generator.DefineLabel();
                var labelNum0036 = generator.DefineLabel();
                // find the method arg that takes Pawn
                var pawnParam =
                    (from param in original.GetParameters() where param.ParameterType == typeof(Pawn) select param).First();
                // store which method we are patching currently
                var isHead = original.Name == "GetHumanlikeHeadSetForPawn";
                //[0000] PUSH bool, ModsConfig.BiotechActive
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModsConfig), "get_BiotechActive"));
                //[0005] POP bool, Jump if false to [0031]
                yield return new CodeInstruction(OpCodes.Brfalse_S, labelNum0031);
                //[0007] PUSH param with type pawn
                yield return new CodeInstruction(OpCodes.Ldarg, pawnParam.Position);
                //[0008] PUSH field Pawn.ageTracker with type Pawn_AgeTracker
                yield return new CodeInstruction(CodeInstruction.LoadField(typeof(Pawn), "ageTracker"));
                //[000D] PUSH Pawn.ageTracker.curLifeStage POP Pawn.ageTracker
                yield return new CodeInstruction(OpCodes.Callvirt,
                AccessTools.Method(typeof(Pawn_AgeTracker), "get_CurLifeStage"));
                //[0012] PUSH ageTracker.curLifeStage.bodyWidth POP ageTracker.curLifeStage
                yield return new CodeInstruction(CodeInstruction.LoadField(typeof(LifeStageDef), "bodyWidth"));
                //[0017] POP bodyWidth to local
                yield return new CodeInstruction(OpCodes.Stloc, localNullableFloat.LocalIndex);
                //[0018] PUSH bodyWidth address
                yield return new CodeInstruction(OpCodes.Ldloca_S, localNullableFloat.LocalIndex);
                //[001A] PUSH bool bodyWidth.hasValue POP address
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(float?), "get_HasValue"));
                //[001F] POP bool jump if true to [0028]
                yield return new CodeInstruction(OpCodes.Brtrue_S, labelNum0028);
                //[0021] PUSH 1.5f
                yield return new CodeInstruction(OpCodes.Ldc_R4, 1.5f);
                //[0026] jump to [0036]
                yield return new CodeInstruction(OpCodes.Br_S, labelNum0036);
                //[0028] PUSH bodyWidth address
                yield return new CodeInstruction(OpCodes.Ldloca_S, localNullableFloat.LocalIndex).WithLabels(labelNum0028);
                //[002A] PUSH float bodyWidth.GetValueOrDefault() POP address
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(float?), "GetValueOrDefault"));
                //[002F] jump to [0036]
                yield return new CodeInstruction(OpCodes.Br_S, labelNum0036);
                //[0031] PUSH 1.5f
                yield return new CodeInstruction(OpCodes.Ldc_R4, 1.5f).WithLabels(labelNum0031);
                // label placeholder so we can branch here and VEF doesn't clobber us
                yield return new CodeInstruction(OpCodes.Nop).WithLabels(labelNum0036);
                if (hasVEF)
                {
                    //[0036] PUSH result of MeshPool.GetMeshSetForWidth(stack float), POP float
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(MeshPool), "GetMeshSetForWidth", new[] {typeof(float)}));
                }
                else
                {
                    // Add our pawn to the stack, we now have two items in the stack - our pawn[0] and our float[1]
                    yield return new CodeInstruction(OpCodes.Ldarg, pawnParam.Position);
                    //[0036] Add a bool to the stack - whether this is head or body
                    yield return new CodeInstruction(OpCodes.Ldc_I4, isHead ? 1 : 0);
                    //[0036] PUSH our custom mesh, POP pawn and float and bool
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(RenderPatches), nameof(GetHeadBodyMeshForPawn)));
                }

                //[003B] RET GraphicMeshSet POP GraphicMeshSet
                yield return new CodeInstruction(OpCodes.Ret);
            }
        }

        [HarmonyPatch]
        //[HarmonyDebug]
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
                // Outcome is essentially:
                // return GetHairBeardMeshForPawn(pawn.story.headType.beardMeshSize * (!ModsConfig.BiotechActive ? 1f : pawn.ageTracker.CurLifeStage.headSizeFactor ?? 1f), pawn, isHair);

                var localNullableFloat = generator.DeclareLocal(typeof(float?));
                // These are required for jumps
                var labelNum004C = generator.DefineLabel();
                var labelNum005A = generator.DefineLabel();
                var labelNum005F = generator.DefineLabel();
                // find the method arg that takes Pawn
                var pawnParam =
                    (from param in original.GetParameters() where param.ParameterType == typeof(Pawn) select param)
                    .First();
                // store which method we are patching currently
                var isHair = original.Name == "GetHumanlikeHairSetForPawn";
                //[0000] PUSH Pawn from arg
                yield return new CodeInstruction(OpCodes.Ldarg, pawnParam.Position);
                //[0004] PUSH field pawn.story POP pawn
                yield return new CodeInstruction(CodeInstruction.LoadField(typeof(Pawn), "story"));
                //[0009] PUSH field story.headType, POP pawn.story
                yield return new CodeInstruction(CodeInstruction.LoadField(typeof(Pawn_StoryTracker), "headType"));
                //[000E] PUSH field headType.beardMeshSize, POP story.headType
                yield return new CodeInstruction(CodeInstruction.LoadField(typeof(HeadTypeDef),
                    isHair ? "hairMeshSize" : "beardMeshSize"));
                //[0013] PUSH bool, ModsConfig.BiotechActive
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(ModsConfig), "get_BiotechActive"));
                //[00018] POP bool, Jump if false to [005A]
                yield return new CodeInstruction(OpCodes.Brfalse_S, labelNum005A);
                //[0001D] PUSH param with type pawn
                yield return new CodeInstruction(OpCodes.Ldarg, pawnParam.Position);
                //[00021] PUSH field Pawn.ageTracker with type Pawn_AgeTracker
                yield return new CodeInstruction(CodeInstruction.LoadField(typeof(Pawn), "ageTracker"));
                //[0026] PUSH Pawn.ageTracker.curLifeStage POP Pawn.ageTracker
                yield return new CodeInstruction(OpCodes.Callvirt,
                    AccessTools.Method(typeof(Pawn_AgeTracker), "get_CurLifeStage"));
                //[002B] PUSH ageTracker.curLifeStage.headSizeFactor POP ageTracker.curLifeStage
                yield return new CodeInstruction(CodeInstruction.LoadField(typeof(LifeStageDef), "headSizeFactor"));
                //[0030] POP headSizeFactor to local
                yield return new CodeInstruction(OpCodes.Stloc, localNullableFloat.LocalIndex);
                //[0034] PUSH headSizeFactor address
                yield return new CodeInstruction(OpCodes.Ldloca_S, localNullableFloat.LocalIndex);
                //[0038] PUSH bool headSizeFactor.hasValue POP address
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(float?), "get_HasValue"));
                //[003D] POP bool jump if true to [004C]
                yield return new CodeInstruction(OpCodes.Brtrue_S, labelNum004C);
                //[0042] PUSH 1f
                yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
                //[0047] jump to [005F]
                yield return new CodeInstruction(OpCodes.Br_S, labelNum005F);
                //[004C] PUSH headSizeFactor address
                yield return new CodeInstruction(OpCodes.Ldloca_S, localNullableFloat.LocalIndex).WithLabels(
                    labelNum004C);
                //[0050] PUSH float headSizeFactor.GetValueOrDefault() POP address
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(float?), "GetValueOrDefault"));
                //[0055] jump to [005F]
                yield return new CodeInstruction(OpCodes.Br_S, labelNum005F);
                //[005A] PUSH 1f
                yield return new CodeInstruction(OpCodes.Ldc_R4, 1f).WithLabels(labelNum005A);
                //[005F] PUSH Vector2 Vector2.op_Multiply(beardMeshSize * scale), POP beardMeshSize and float
                yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(Vector2), "op_Multiply", new[] {typeof(Vector2), typeof(float)}))
                    .WithLabels(labelNum005F);
                //[0064]
                // Add our pawn to the stack, we now have two items in the stack - our pawn[0] and our float[1]
                yield return new CodeInstruction(OpCodes.Ldarg, pawnParam.Position);
                //[0068] Add a bool to the stack - whether this is hair or beard (for caching purposes)
                yield return new CodeInstruction(OpCodes.Ldc_I4, isHair ? 1 : 0);
                //[006D] PUSH our custom mesh, POP pawn, Vector2, bool
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(RenderPatches), nameof(GetHairBeardMeshForPawn)));
                //[0072] RET GraphicMeshSet POP GraphicMeshSet
                yield return new CodeInstruction(OpCodes.Ret);
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
    [HarmonyAfter("OskarPotocki.VFECore")]
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
    }
}