using System.Reflection;
using HarmonyLib;
using Verse;

namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    // We're just going to patch ourselves if it's relevant
    [HarmonyPatch]
    public static class VefCompatibilityPatches
    {
        private static readonly BodyScaleDelegate vefBodyScaleMethod = hasVef
            ? AccessTools.MethodDelegate<BodyScaleDelegate>(
                "VanillaGenesExpanded.HumanlikeMeshPoolUtility_Patches:GeneScaleFactor")
            : null;


        public static bool Prepare()
        {
            return hasVef && NotNull(vefBodyScaleMethod);
        }


        public static MethodBase TargetMethod()
        {
            return AccessTools.Method("VariedBodySizes.VariedBodySizes_GameComponent:OnCalculateBodySize");
        }


        public static float Postfix(float result, Pawn pawn)
        {
            return pawn is null ? result : vefBodyScaleMethod(result, pawn);
        }

        private delegate float BodyScaleDelegate(float width, Pawn pawn);
    }
}