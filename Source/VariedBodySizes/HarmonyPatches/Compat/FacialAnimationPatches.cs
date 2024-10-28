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

        private static GraphicMeshSet GetBodyOverlayMeshForPawn(GraphicMeshSet baseMesh, Pawn pawn)
        {
            if (HeadCache.TryGet(pawn, out var returnedMesh))
            {
                return returnedMesh;
            }

            var result = Main.TranslateForPawn(baseMesh, pawn);
            HeadCache.Set(pawn, result);
            return result;
        }


        public static bool Prepare()
        {
            return ModsConfig.IsActive("Nals.FacialAnimation") && NotNull(getHeadMeshSet);
        }

        public static MethodBase TargetMethod()
        {
            return getHeadMeshSet;
        }

        public static void Postfix(ref GraphicMeshSet __result, Pawn pawn)
        {
            __result = GetBodyOverlayMeshForPawn(__result, pawn);
        }
    }
}