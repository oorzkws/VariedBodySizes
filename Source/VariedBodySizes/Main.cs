using System.Diagnostics;
using System.Reflection.Emit;
using Log = Verse.Log;

namespace VariedBodySizes;

[StaticConstructorOnStartup]
public static class Main
{
    public static VariedBodySizes_GameComponent CurrentComponent;
    public static readonly List<ThingDef> AllPawnTypes;
    public static readonly Harmony HarmonyInstance;

    static Main()
    {
        AllPawnTypes = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.race != null)
            .OrderBy(def => def.label).ToList();
        HarmonyInstance = new Harmony("Mlie.VariedBodySizes");
        // Until VEF changes their mind we're just going to override with our own scaling.
        foreach (var targetPair in new[]
                 {
                     new KeyValuePair<Type, string>(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeBodySetForPawn"),
                     new KeyValuePair<Type, string>(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeHeadSetForPawn"),
                     //new(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeHairSetForPawn"),
                     //new(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeBeardSetForPawn"),
                     //new(typeof(HumanlikeMeshPoolUtility), "HumanlikeBodyWidthForPawn"),
                     //new(typeof(PawnRenderer), "GetBodyOverlayMeshSet"),
                     new(typeof(PawnRenderer), "BaseHeadOffsetAt")
                     //new(typeof(PawnRenderer), "DrawBodyApparel"),
                     //new(typeof(PawnRenderer), "DrawBodyGenes"),
                     //new(typeof(GeneGraphicData), "GetGraphics"),
                     //new(AccessTools.TypeByName("Verse.PawnRenderer+<>c__DisplayClass54_0"),
                     //    "<DrawHeadHair>g__DrawExtraEyeGraphic|6")
                 })
        {
            var targetMethod = AccessTools.Method(targetPair.Key, targetPair.Value);
            if (targetMethod == null)
            {
                continue;
            }

            var patches = Harmony.GetPatchInfo(targetMethod);
            if (patches?.Owners.Contains("OskarPotocki.VFECore") != true)
            {
                continue;
            }

            LogMessage($"Unpatching {targetMethod.DeclaringType?.Name ?? string.Empty}:{targetMethod.Name}", true);
            HarmonyInstance.Unpatch(targetMethod, HarmonyPatchType.All, "OskarPotocki.VFECore");
        }

        // Do our patches after we undo theirs
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        VariedBodySizesMod.instance.Settings.VariedBodySizes ??= new Dictionary<string, FloatRange>();
    }

    public static float GetPawnVariation(Pawn pawn)
    {
        var sizeRange = VariedBodySizesMod.instance.Settings.DefaultVariation;
        if (VariedBodySizesMod.instance.Settings.VariedBodySizes.TryGetValue(pawn.def.defName, out var bodySize))
        {
            sizeRange = bodySize;
        }

        return (float)Math.Round(Rand.Range(sizeRange.min, sizeRange.max), 2);
    }

    public static void ResetAllCaches(Pawn pawn)
    {
        if (HarmonyPatches.FacialAnimation_GetHeadMeshSetPatch.HeadCache.TryGet(pawn, out _))
        {
            HarmonyPatches.FacialAnimation_GetHeadMeshSetPatch.HeadCache.Remove(pawn);
        }

        if (HarmonyPatches.Pawn_BodySizePatch.StatCache.TryGet(pawn, out _))
        {
            HarmonyPatches.Pawn_BodySizePatch.StatCache.Remove(pawn);
        }

        if (HarmonyPatches.PawnRenderer_GetBodyOverlayMeshSetPatch.OverlayCache.TryGet(pawn, out _))
        {
            HarmonyPatches.PawnRenderer_GetBodyOverlayMeshSetPatch.OverlayCache.Remove(pawn);
        }

        GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
    }

    public static void LogMessage(string message, bool forced = false)
    {
        if (!forced && !VariedBodySizesMod.instance.Settings.VerboseLogging)
        {
            return;
        }

        Log.Message($"[VariedBodySizes]: {message}");
    }
}

// Utility stuff here

public static partial class HarmonyPatches
{
    // We have to overwrite their patches, unfortunately
    private static bool hasVef => ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core");

    private static float GetScalarForPawn(Pawn pawn)
    {
        return Main.CurrentComponent?.GetVariedBodySize(pawn) ?? 1f;
    }

    private static bool NotNull(params object[] input)
    {
        if (input.All(o => o is not null))
        {
            return true;
        }

        Main.LogMessage("Signature match not found", true);
        foreach (var obj in input)
        {
            if (obj is MemberInfo memberObj)
            {
                Main.LogMessage($"\tValid entry:{memberObj}", true);
            }
        }

        return false;
    }

    // CodeMatcher will throw errors if we try to take actions in an invalid state (i.e. no match)
    private static CodeMatcher OnSuccess(this CodeMatcher match, Action<CodeMatcher> action, bool suppress = false)
    {
        switch (match.IsInvalid)
        {
            case true when !suppress:
                Main.LogMessage(
                    $"Transpiler did not find target @ {new StackTrace().GetFrame(1).GetMethod().DeclaringType?.FullName ?? "unknown"}",
                    true);
                break;
            case false:
                action.Invoke(match);
                break;
        }

        return match;
    }

    /// <summary>
    ///     Replaces the pattern with the replacement. This is a naive implementation - if you need labels, DIY it.
    /// </summary>
    /// <param name="match">the CodeMatcher instance to use</param>
    /// <param name="pattern">instructions to match, the beginning of this match is where the replacement begins</param>
    /// <param name="replacement">the new instructions</param>
    /// <param name="replace">
    ///     Whether we should keep labels and set the opcodes/operands instead of recreating the
    ///     CodeInstruction
    /// </param>
    /// <param name="suppress">Whether to suppress the log message on a failed match</param>
    /// <returns></returns>
    private static CodeMatcher Replace(this CodeMatcher match, CodeMatch[] pattern, CodeInstructions replacement,
        bool replace = true, bool suppress = false)
    {
        return match.MatchStartForward(pattern).OnSuccess(matcher =>
        {
            var newOps = replacement.ToList();
            for (var i = 0; i < Math.Max(newOps.Count, pattern.Length); i++)
            {
                if (i < newOps.Count)
                {
                    var op = newOps[i];
                    if (i < pattern.Length)
                    {
                        if (replace)
                        {
                            // This keeps labels
                            matcher.SetAndAdvance(op.opcode, op.operand);
                        }
                        else
                        {
                            matcher.RemoveInstruction();
                            matcher.InsertAndAdvance(op);
                        }
                    }
                    else
                    {
                        matcher.InsertAndAdvance(op);
                    }
                }
                else
                {
                    matcher.RemoveInstruction();
                }
            }
        }, suppress);
    }

    /// <summary>
    ///     Returns the given method as a basic ILCode signature, excluding ret statements
    /// </summary>
    /// <param name="method">The method to convert</param>
    /// <returns>A set of CodeInstructions representing the method given</returns>
    private static CodeInstructions InstructionSignature(Delegate method)
    {
        // arg indexes get shifted by 1 if we use this method, so we have to shift them back
        return PatchProcessor.GetCurrentInstructions(method.Method)
            .Manipulator(
                i => i.IsLdarg() || i.IsStarg(),
                i =>
                {
                    var index = new FishInstruction(i).GetIndex() - 1;
                    var copy = i.IsLdarg() ? FishTranspiler.Argument(index) : FishTranspiler.StoreArgument(index);
                    // Replace the opcode and operand with the proper one
                    i.With(copy.OpCode, copy.Operand);
                })
            .Where(i => i.opcode != OpCodes.Ret && i.opcode != OpCodes.Nop);
    }

    /// <summary>
    ///     Returns the given method as a basic ILCode signature wrapped in a CodeMatch, excluding ret statements
    /// </summary>
    /// <param name="method">The method to convert</param>
    /// <returns>A set of CodeInstructions representing the method given</returns>
    private static CodeMatch[] InstructionMatchSignature(Delegate method)
    {
        return InstructionSignature(method).Select(i => new CodeMatch(i.opcode, i.operand)).ToArray();
    }
}