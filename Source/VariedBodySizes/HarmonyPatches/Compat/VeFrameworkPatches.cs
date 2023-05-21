namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    // We're just going to patch ourselves if it's relevant
    [HarmonyPatch]
    public static class VeFramework_OnCalculateBodySizePatch
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


        public static void Postfix(ref float __result, Pawn pawn)
        {
            if (pawn is not null)
            {
                __result = vefBodyScaleMethod(__result, pawn);
            }
        }

        private delegate float BodyScaleDelegate(float width, Pawn pawn);
    }
}