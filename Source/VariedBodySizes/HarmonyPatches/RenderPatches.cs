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

    [HarmonyPatch(typeof(PawnDrawUtility), "CalcAnchorData")]
    public static class PawnDrawUtility_CalcAnchorDataPatch
    {
        public static void Postfix(Pawn pawn, Rot4 pawnRot, ref Vector3 anchorOffset)
        {
            var scale = GetScalarForPawn(pawn);
            anchorOffset = anchorOffset.ScaledBy(new Vector3(scale, 1f, 1f / Mathf.Sqrt(scale * 1.2f)));
        }
    }

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
    public static class HumanlikeMeshPoolUtility_HumanlikeHeadWidthForPawnPatch
    {
        private static readonly MethodBase humanlikeHeadWidthForPawn =
            AccessTools.Method(typeof(HumanlikeMeshPoolUtility), "HumanlikeHeadWidthForPawn");

        public static bool Prepare()
        {
            return NotNull(humanlikeHeadWidthForPawn);
        }


        public static MethodBase TargetMethod()
        {
            return humanlikeHeadWidthForPawn;
        }


        /*public static void Postfix(Pawn pawn, ref float __result)
        {
            __result *= GetScalarForPawn(pawn);
        }*/

        public static CodeInstructions Transpiler(CodeInstructions instructions, MethodBase method)
        {
            var editor = new CodeMatcher(instructions);

            var fixedWidth = InstructionMatchSignature(() => 1.5f);
            var newGetMeshSetForWidth = InstructionSignature((Pawn pawn) =>
                // ReSharper disable once ConvertClosureToMethodGroup - accepting this suggestion breaks the signature
                1.5f * GetScalarForPawn(pawn)
            ); // We reference this multiple times, so we .ToArray to avoid multi-enumeration issues.
            editor.Start().Replace(fixedWidth, newGetMeshSetForWidth, suppress: true);

            return editor.InstructionEnumeration();
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

        private static readonly Dictionary<Pawn, Dictionary<Hediff, Vector2>> originalHediffSizes =
            new Dictionary<Pawn, Dictionary<Hediff, Vector2>>();

        private static readonly Dictionary<Pawn, Dictionary<Trait, Vector2>> originalTraitSizes =
            new Dictionary<Pawn, Dictionary<Trait, Vector2>>();

        private static readonly IEnumerable<FieldInfo> graphicFields = AccessTools
            .GetDeclaredFields(typeof(PawnRenderTree))
            .Where(f => f.FieldType == typeof(Graphic));

        private static readonly MethodBase resolveAllGraphics =
            AccessTools.Method(typeof(PawnRenderTree), "TrySetupGraphIfNeeded");

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

        private static void processNode(ref PawnRenderNode node, ref Pawn pawn, ref float pawnDrawSize)
        {
            for (var i = 0; i < (node.children?.Length ?? 0); i++)
            {
                processNode(ref node.children![i], ref pawn, ref pawnDrawSize);
            }

            // my brain is too fried rn to deduplicate this

            if (node.apparel is not null)
            {
                var graphic = node.graphic;

                // If it doesn't match we won't have a rotting graphic anyway
                if (!ProcessField(originalGearSizes[pawn], node.apparel, node.graphic, pawnDrawSize,
                        out var scaledGraphic))
                {
                    return;
                }

                // Pop back and continue
                node.graphic = scaledGraphic;
            }

            if (node.hediff is not null)
            {
                var graphic = node.graphic;

                // If it doesn't match we won't have a rotting graphic anyway
                if (!ProcessField(originalHediffSizes[pawn], node.hediff, node.graphic, pawnDrawSize,
                        out var scaledGraphic))
                {
                    return;
                }

                // Pop back and continue
                node.graphic = scaledGraphic;
            }

            if (node.gene is not null)
            {
                var graphic = node.graphic;

                // If it doesn't match we won't have a rotting graphic anyway
                if (!ProcessField(originalGeneSizes[pawn], node.gene, node.graphic, pawnDrawSize,
                        out var scaledGraphic))
                {
                    return;
                }

                // Pop back and continue
                node.graphic = scaledGraphic;
                ;
            }

            if (node.trait is not null)
            {
                var graphic = node.graphic;

                // If it doesn't match we won't have a rotting graphic anyway
                if (!ProcessField(originalTraitSizes[pawn], node.trait, node.graphic, pawnDrawSize,
                        out var scaledGraphic))
                {
                    return;
                }

                // Pop back and continue
                node.graphic = scaledGraphic;
            }
        }

        public static bool Prepare()
        {
            return NotNull(resolveAllGraphics);
        }


        public static MethodBase TargetMethod()
        {
            return resolveAllGraphics;
        }


        public static void Postfix(PawnRenderTree __instance, Pawn ___pawn)
        {
            var pawnDrawSize = GetScalarForPawn(___pawn);

            // Build dict entry if not already present
            if (!originalSizes.ContainsKey(___pawn))
            {
                originalSizes.Add(___pawn, new Dictionary<FieldInfo, Vector2>());
                originalGeneSizes.Add(___pawn, new Dictionary<Gene, Vector2>());
                originalGearSizes.Add(___pawn, new Dictionary<Apparel, Vector2>());
                originalHediffSizes.Add(___pawn, new Dictionary<Hediff, Vector2>());
                originalTraitSizes.Add(___pawn, new Dictionary<Trait, Vector2>());
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

            // "Child" graphics - genes, hediffs, apparel
            processNode(ref __instance.rootNode, ref ___pawn, ref pawnDrawSize);

            // Done
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
            var pattern = InstructionMatchSignature((Pawn pawn) => pawn.story.headType.hairMeshSize);
            var replacement =
                InstructionSignature((Pawn pawn) => GetScalarForPawn(pawn) * pawn.story.headType.hairMeshSize);
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
            var pattern = InstructionMatchSignature((Pawn pawn) => pawn.story.headType.beardMeshSize);
            var replacement =
                InstructionSignature((Pawn pawn) => GetScalarForPawn(pawn) * pawn.story.headType.beardMeshSize);
            editor.Start().Replace(pattern, replacement);

            return editor.InstructionEnumeration();
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
                Pin(ref size);
            });
            var replacement = InstructionSignature((PawnRenderer self) =>
            {
                var size = self.pawn.story.bodyType.headOffset *
                           Mathf.Sqrt(self.pawn.ageTracker.CurLifeStage.bodySizeFactor) * GetScalarForPawn(self.pawn);
                Pin(ref size);
            });
            editor.Start().Replace(pattern, replacement);

            return editor.InstructionEnumeration();
        }
    }
}