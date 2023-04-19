namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    // It uses a custom head mesh so we have to modify it a little
    [HarmonyPatch]
    public static class FacialAnimation_GetHeadMeshSetPatch
    {
        private static readonly MethodBase getHeadMeshSet =
            AccessTools.Method("FacialAnimation.GraphicHelper:GetHeadMeshSet");

        public static readonly TimedCache<GraphicMeshSet> HeadCache = new TimedCache<GraphicMeshSet>(360);

        private static GraphicMeshSet TranslateForPawn(GraphicMeshSet baseMesh, Pawn pawn)
        {
            // North[2] is positive on both x and y axis
            var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2 * GetScalarForPawn(pawn);
            return MeshPool.GetMeshSetForWidth(baseVector.x, baseVector.z);
        }

        private static GraphicMeshSet GetBodyOverlayMeshForPawn(GraphicMeshSet baseMesh, Pawn pawn)
        {
            if (HeadCache.TryGet(pawn, out var returnedMesh))
            {
                return returnedMesh;
            }

            var result = TranslateForPawn(baseMesh, pawn);
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