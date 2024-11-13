namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    // It uses a custom head mesh, so we have to modify it a little
    [HarmonyPatch]
    public static class FacialAnimation_GetHeadMeshSetPatch
    {
        private static readonly MethodBase getHeadMeshSet =
            AccessTools.Method("FacialAnimation.GraphicHelper:GetHeadMeshSet");

        public static readonly TimedCache<GraphicMeshSet> HeadCache = new TimedCache<GraphicMeshSet>(360);

        public static bool Prepare()
        {
            return ModsConfig.IsActive("Nals.FacialAnimation") && NotNull(getHeadMeshSet);
        }

        public static MethodBase TargetMethod()
        {
            return getHeadMeshSet;
        }

        public static void Postfix(ref Vector2 __result, Pawn pawn)
        {
            __result *= GetScalarForPawn(pawn);
        }
    }
}