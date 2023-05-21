using System.Diagnostics;
using System.Reflection.Emit;

namespace VariedBodySizes;

/// <summary>
///     Class containing Harmony patches used by the mod
/// </summary>
/// <remarks>Shared functions are defined in HarmonyPatches/HarmonyPatches.cs</remarks>
public static partial class HarmonyPatches
{
    // We have to overwrite their patches, unfortunately
    private static bool hasVef => ModsConfig.IsActive("oskarpotocki.vanillafactionsexpanded.core");

    /// <summary>
    ///     This is only called once from Main to do our patching.
    /// </summary>
    public static void ApplyAll(Harmony harmony)
    {
        // Most of the VEF render patches co-exist fine, these don't.
        var vePatchesToUndo = new[]
        {
            new KeyValuePair<Type, string>(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeBodySetForPawn"),
            new KeyValuePair<Type, string>(typeof(HumanlikeMeshPoolUtility), "GetHumanlikeHeadSetForPawn"),
            new KeyValuePair<Type, string>(typeof(PawnRenderer), "BaseHeadOffsetAt")
        };
        // Go through the problem methods, if they have a VEF-originated patch then remove it
        foreach (var targetPair in vePatchesToUndo)
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

            Main.LogMessage($"Unpatching {targetMethod.DeclaringType?.Name ?? string.Empty}:{targetMethod.Name}", true);
            harmony.Unpatch(targetMethod, HarmonyPatchType.All, "OskarPotocki.VFECore");
        }

        // Do our patches after we undo theirs
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }


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

    /// <summary>
    ///     Returns the type that called a transpiler in a given stack trace
    /// </summary>
    /// <returns></returns>
    private static string GetTranspilerStackFrame()
    {
        var trace = new StackTrace();
        foreach (var frame in trace.GetFrames()!)
        {
            var method = frame.GetMethod();
            if (method.Name == "Transpiler")
            {
                return method.DeclaringType?.FullName ?? "unknown";
            }
        }

        return "unknown";
    }

    // CodeMatcher will throw errors if we try to take actions in an invalid state (i.e. no match)
    private static CodeMatcher OnSuccess(this CodeMatcher match, Action<CodeMatcher> action, bool suppress = false)
    {
        switch (match.IsInvalid)
        {
            case true when !suppress:
                Main.LogMessage(
                    $"Transpiler did not find target @ {GetTranspilerStackFrame()}",
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
    // ReSharper disable once UnusedMethodReturnValue.Local
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