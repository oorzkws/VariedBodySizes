using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    // We're just going to patch ourselves if it's relevant
    [HarmonyPatch]
    [UsedImplicitly]
    private static class VefCompatPatch
    {
        private delegate float BodyScaleDelegate(float width, Pawn pawn);

        private static readonly BodyScaleDelegate vefBodyScaleMethod = hasVef ? AccessTools.MethodDelegate<BodyScaleDelegate>(
            "VanillaGenesExpanded.HumanlikeMeshPoolUtility_Patches:GeneScaleFactor") : null;

        [UsedImplicitly]
        private static bool Prepare() => hasVef && NotNull(vefBodyScaleMethod);
        
        [UsedImplicitly]
        private static MethodBase TargetMethod() =>
            AccessTools.Method("VariedBodySizes.VariedBodySizes_GameComponent:OnCalculateBodySize");

        [UsedImplicitly]
        private static float Postfix(float result, Pawn pawn)
        {
            return pawn is null ? result : vefBodyScaleMethod(result, pawn);
        }
    }
    
}