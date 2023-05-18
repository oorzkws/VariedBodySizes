using System.Reflection.Emit;

namespace VariedBodySizes;

// Note to self: args(oldest, newer, newest)
// These patches are the most likely to break with time, so they're all wrapped with Prepare()

[HarmonyBefore("OskarPotocki.VFECore")]
public static partial class HarmonyPatches
{
    private static readonly MethodBase getHumanlikeBodyWidthFunc =
        AccessTools.Method(typeof(HumanlikeMeshPoolUtility), "HumanlikeBodyWidthForPawn");

    private static readonly FieldInfo pawnRendererPawn = AccessTools.Field("Verse.PawnRenderer:pawn");

    private static readonly MethodBase getScalar =
        AccessTools.Method(typeof(HarmonyPatches), "GetScalarForPawn");

    [HarmonyPatch]
    public static class HumanlikeMeshPoolUtility_HumanlikeBodyWidthForPawnPatch
    {
        public static bool Prepare()
        {
            return NotNull(getHumanlikeBodyWidthFunc);
        }


        public static MethodBase TargetMethod()
        {
            return getHumanlikeBodyWidthFunc;
        }


        public static void Postfix(Pawn pawn, ref float __result)
        {
            __result *= GetScalarForPawn(pawn);
        }
    }

    [HarmonyPatch]
    public static class PawnGraphicSet_ResolveAllGraphicsPatch
    {
        private static readonly Dictionary<Pawn, Dictionary<FieldInfo, Vector2>> originalSizes =
            new Dictionary<Pawn, Dictionary<FieldInfo, Vector2>>();

        private static readonly Dictionary<Pawn, Dictionary<Gene, Vector2>> originalGeneSizes =
            new Dictionary<Pawn, Dictionary<Gene, Vector2>>();

        private static readonly Dictionary<Pawn, Dictionary<Apparel, Vector2>> originalGearSizes =
            new Dictionary<Pawn, Dictionary<Apparel, Vector2>>();

        private static readonly IEnumerable<FieldInfo> graphicFields = AccessTools
            .GetDeclaredFields(typeof(PawnGraphicSet))
            .Where(f => f.FieldType == typeof(Graphic));

        private static readonly MethodBase resolveAllGraphics =
            AccessTools.Method(typeof(PawnGraphicSet), "ResolveAllGraphics");

        private static bool ProcessField<TKey>(Dictionary<TKey, Vector2> backingDict, TKey key, Graphic graphic,
            float pawnScale, out Graphic scaledGraphic)
        {
            scaledGraphic = default;
            if (key is null)
            {
                return false;
            }

            if (graphic is null)
            {
                return false;
            }

            backingDict.TryAdd(key, graphic.drawSize);

            var originalSize = backingDict[key];
            var scaledSize = originalSize * pawnScale;

            // Nothing to do
            if (originalSize == scaledSize)
            {
                return false;
            }

            if (scaledSize == graphic.drawSize)
            {
                return false;
            }

            // Graphic was changed somehow
            if (originalSize != graphic.drawSize)
            {
                originalSize = graphic.drawSize;
                scaledSize = originalSize * pawnScale;
                backingDict[key] = originalSize;
            }

            // we could graphic.GetCopy(scaledSize, null) but that discards the mask :(
            scaledGraphic = GraphicDatabase.Get(graphic.GetType(), graphic.path, graphic.Shader, scaledSize,
                graphic.color, graphic.colorTwo, graphic.maskPath);
            return true;
        }


        public static bool Prepare()
        {
            return NotNull(resolveAllGraphics);
        }


        public static MethodBase TargetMethod()
        {
            return resolveAllGraphics;
        }


        public static void Postfix(PawnGraphicSet __instance, Pawn ___pawn)
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
                        out var scaledGraphic))
                {
                    continue;
                }

                // Resize and continue
                field.SetValue(__instance, scaledGraphic);
            }

            // Gene graphics like eye overlays
            for (var i = 0; i < (__instance.geneGraphics?.Count ?? 0); i++)
            {
                var geneGraphic = __instance.geneGraphics![i];

                // If it doesn't match we won't have a rotting graphic anyway
                if (!ProcessField(originalGeneSizes[___pawn], geneGraphic.sourceGene, geneGraphic.graphic, pawnDrawSize,
                        out var scaledGraphic))
                {
                    continue;
                }

                geneGraphic.graphic = scaledGraphic;

                if (ProcessField(originalGeneSizes[___pawn], geneGraphic.sourceGene, geneGraphic.rottingGraphic,
                        pawnDrawSize,
                        out var scaledRottingGraphic))
                {
                    geneGraphic.rottingGraphic = scaledRottingGraphic;
                }

                // Pop back and continue
                __instance.geneGraphics[i] = new GeneGraphicRecord(geneGraphic.graphic,
                    geneGraphic.rottingGraphic, geneGraphic.sourceGene);
            }

            // Apparel graphics 
            for (var i = 0; i < (__instance.apparelGraphics?.Count ?? 0); i++)
            {
                var gearGraphic = __instance.apparelGraphics![i];
                if (!ProcessField(originalGearSizes[___pawn], gearGraphic.sourceApparel, gearGraphic.graphic,
                        pawnDrawSize,
                        out var scaledGraphic))
                {
                    continue;
                }

                // Pop back and continue
                __instance.apparelGraphics[i] =
                    new ApparelGraphicRecord(scaledGraphic, gearGraphic.sourceApparel);
            }
            // Done
        }
    }

    [HarmonyPatch]
    [HarmonyAfter("rimworld.erdelf.alien_race.main")]
    public static class GraphicMeshSet_GetHumanlikeSetForPawnPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeHeadSetForPawn");
            yield return AccessTools.Method("Verse.HumanlikeMeshPoolUtility:GetHumanlikeBodySetForPawn");
        }

        public static CodeInstructions Transpiler(CodeInstructions instructions, MethodBase method)
        {
            var editor = new CodeMatcher(instructions);

            // First change: MeshPool.GetMeshSetForWidth(pawn.ageTracker.CurLifeStage.bodyWidth.Value) -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
            var getMeshSetForWidth = InstructionMatchSignature((Pawn pawn) =>
                pawn.ageTracker.CurLifeStage.bodyWidth!.Value
            );
            var newGetMeshSetForWidth = InstructionSignature((Pawn pawn) =>
                // ReSharper disable once ConvertClosureToMethodGroup - accepting this suggestion breaks the signature
                HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn(pawn)
            ).ToArray(); // We reference this multiple times, so we .ToArray to avoid multi-enumeration issues.
            editor.Start().Replace(getMeshSetForWidth, newGetMeshSetForWidth);

            // Second change: MeshPool.humanlikeHead/BodySet -> MeshPool.GetMeshSetForWidth(MeshPool.HumanlikeBodyWidthForPawn(pawn))
            // May fail if HAR is present
            var hasHAR = ModsConfig.IsActive("erdelf.humanoidalienraces");
            var getSet = method.Name.Contains("Head")
                ? InstructionMatchSignature(() => MeshPool.humanlikeHeadSet)
                : InstructionMatchSignature(() => MeshPool.humanlikeBodySet);
            var newGetSet = InstructionSignature((Pawn pawn) =>
                MeshPool.GetMeshSetForWidth(HumanlikeMeshPoolUtility.HumanlikeBodyWidthForPawn(pawn))
            );
            editor.Start()
                .Replace(getSet, newGetSet,
                    suppress: hasHAR); // Matching may error with HAR and that's fine, we cover that case below

            // Third change, if HAR is active: (object)1.5f -> MeshPool.HumanlikeBodyWidthForPawn(pawn)
            // ReSharper disable once InvertIf
            if (hasHAR)
            {
                var fixedWidth = InstructionMatchSignature(() => 1.5f);
                // replacement IL is the same as the first edit
                // Note: with [HarmonyAfter], if we're *before* HAR in the load order, our transpiler will run twice:
                // Once on the vanilla code, and then once again on the code HAR has modified.
                // The first will naturally not find the HAR edits, so we suppress the error here.
                editor.Start().Replace(fixedWidth, newGetMeshSetForWidth, suppress:true);
            }

            return editor.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeHairSetForPawn")]
    public static class GraphicMeshSet_GetHairSetForPawnPatch
    {
        public static CodeInstructions Transpiler(CodeInstructions instructions)
        {
            var editor = new CodeMatcher(instructions);

            // Just adding our multiplier in here
            // ReSharper disable all UnusedVariable
            var pattern = InstructionMatchSignature((Pawn pawn) =>
            {
                var hairMeshSize = pawn.story.headType.hairMeshSize;
            });
            var replacement = InstructionSignature((Pawn pawn) =>
            {
                var hairMeshSize = GetScalarForPawn(pawn) * pawn.story.headType.hairMeshSize;
            });
            editor.Start().Replace(pattern, replacement);

            return editor.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeBeardSetForPawn")]
    public static class GraphicMeshSet_GetBeardSetForPawnPatch
    {
        public static CodeInstructions Transpiler(CodeInstructions instructions)
        {
            var editor = new CodeMatcher(instructions);

            // Just adding our multiplier in here
            var pattern = InstructionMatchSignature((Pawn pawn) =>
            {
                var hairMeshSize = pawn.story.headType.beardMeshSize;
            });
            var replacement = InstructionSignature((Pawn pawn) =>
            {
                var hairMeshSize = GetScalarForPawn(pawn) * pawn.story.headType.beardMeshSize;
            });
            editor.Start().Replace(pattern, replacement);

            return editor.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "GetBodyOverlayMeshSet")]
    public static class PawnRenderer_GetBodyOverlayMeshSetPatch
    {
        public static readonly TimedCache<GraphicMeshSet> OverlayCache = new TimedCache<GraphicMeshSet>(360);

        private static GraphicMeshSet TranslateForPawn(GraphicMeshSet baseMesh, Pawn pawn)
        {
            // North[2] is positive on both x and y axis. Defaults would be 0.65,0,0.65 times 2 for the default 1.3f
            var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2 * GetScalarForPawn(pawn);
            return MeshPool.GetMeshSetForWidth(baseVector.x, baseVector.z);
        }

        private static GraphicMeshSet GetBodyOverlayMeshForPawn(GraphicMeshSet baseMesh, Pawn pawn)
        {
            if (OverlayCache.TryGet(pawn, out var returnedMesh))
            {
                return returnedMesh;
            }

            var result = TranslateForPawn(baseMesh, pawn);
            OverlayCache.Set(pawn, result);
            return result;
        }

        public static void Postfix(ref GraphicMeshSet __result, Pawn ___pawn)
        {
            __result = GetBodyOverlayMeshForPawn(__result, ___pawn);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "BaseHeadOffsetAt"), HarmonyBefore("rimworld.erdelf.alien_race.main")]
    public static class PawnRenderer_BaseHeadOffsetAtPatch
    {
        public static CodeInstructions Transpiler(CodeInstructions instructions)
        {
            var editor = new CodeMatcher(instructions);

            // Just adding our multiplier in here
            var pattern = InstructionMatchSignature((PawnRenderer self) =>
            {
                var size = self.pawn.story.bodyType.headOffset *
                           Mathf.Sqrt(self.pawn.ageTracker.CurLifeStage.bodySizeFactor);
            });
            var replacement = InstructionSignature((PawnRenderer self) =>
            {
                var size = self.pawn.story.bodyType.headOffset *
                           Mathf.Sqrt(self.pawn.ageTracker.CurLifeStage.bodySizeFactor * GetScalarForPawn(self.pawn));
            });
            editor.Start().Replace(pattern, replacement);

            return editor.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "DrawBodyGenes")]
    public static class PawnRenderer_DrawBodyGenesPatch
    {
        public static CodeInstructions Transpiler(CodeInstructions instructions)
        {
            var editor = new CodeMatcher(instructions);

            // Just adding our multiplier in here
            var pattern = InstructionMatchSignature((PawnRenderer self) =>
            {
                var size = self.pawn.story.bodyType.bodyGraphicScale;
            });
            var replacement = InstructionSignature((PawnRenderer self) =>
            {
                var size = GetScalarForPawn(self.pawn) * self.pawn.story.bodyType.bodyGraphicScale;
            });
            editor.Start().Replace(pattern, replacement);

            return editor.InstructionEnumeration();
        }
    }

    [HarmonyPatch]
    public static class PawnRenderer_DrawExtraEyeGraphicPatch
    {
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

        private static Vector3 ModifyVectorForPawn(Vector3 vec, Pawn pawn)
        {
            var scalar = GetScalarForPawn(pawn);
            vec.x *= scalar;
            vec.z += vec.z * (1 - scalar) * 0.5f; // +/- 50% * scalar
            return vec;
        }


        public static bool Prepare()
        {
            return NotNull(drawEyeOverlay, eyeOverlayPawnRendererField, pawnRendererPawn, woundOffset);
        }


        public static MethodBase TargetMethod()
        {
            return drawEyeOverlay;
        }


        public static CodeInstructions Transpiler(CodeInstructions instructions)
        {
            var editor = new CodeMatcher(instructions);
            var woundOffsetCount = 0;
            // First change: scale = HarmonyPatches.GetScalarForPawn(@this.<>4__this.pawn) * scale;

            // Second change: Matrix4x4.TRS(... woundAnchorl2.offset ...)
            // |-> Matrix4x4.TRS(... PawnRenderer_DrawExtraEyeGraphicPatch.ModifyVectorForPawn(woundAnchorl.offset, @this.<>4__this.pawn) ...)

            // Modify the 2nd arg - scale (float) - right at the start of the function
            // Since we use repeat() everywhere we don't have to care about checking for validity.. probably
            return editor.Start().InsertAndAdvance(
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
                new CodeMatch(_ => woundOffsetCount++ < 2)
            ).Repeat(match => match.Advance(2).InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, eyeOverlayPawnRendererField),
                new CodeInstruction(OpCodes.Ldfld, pawnRendererPawn),
                new CodeInstruction(OpCodes.Call, modifyVec)
            )).Start().MatchStartForward(
                // vector 3_2, vector 3_3
                new CodeMatch(i =>
                {
                    if (!i.IsLdloc())
                    {
                        return false;
                    }

                    if (i.operand is not LocalBuilder candidateLocal)
                    {
                        return false;
                    }

                    if (candidateLocal.LocalType != typeof(Vector3))
                    {
                        return false;
                    }

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