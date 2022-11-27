using System.Reflection;
using HarmonyLib;
using Verse;

namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    public static class FacialAnimationCompatibilityPatches
    {
        // It uses a custom head mesh so we have to modify it a little
        [HarmonyPatch]
        public static class FacialAnimation_GetHeadMeshSetPatch
        {
            private static readonly MethodBase GetHeadMeshSet =
                AccessTools.Method("FacialAnimation.GraphicHelper:GetHeadMeshSet");

            private static readonly TimedCache<GraphicMeshSet> headCache = new TimedCache<GraphicMeshSet>(360);


            private static GraphicMeshSet TranslateForPawn(GraphicMeshSet baseMesh, Pawn pawn)
            {
                // North[2] is positive on both x and y axis
                var baseVector = baseMesh.MeshAt(Rot4.North).vertices[2] * 2 * GetScalarForPawn(pawn);
                return MeshPool.GetMeshSetForWidth(baseVector.x, baseVector.z);
            }

            private static GraphicMeshSet GetBodyOverlayMeshForPawn(GraphicMeshSet baseMesh, Pawn pawn)
            {
                if (!headCache.TryGet(pawn, out var returnedMesh))
                {
                    return headCache.SetAndReturn(pawn, TranslateForPawn(baseMesh, pawn));
                }

                return returnedMesh;
            }


            public static bool Prepare()
            {
                return ModsConfig.IsActive("Nals.FacialAnimation") && NotNull(GetHeadMeshSet);
            }


            public static MethodBase TargetMethod()
            {
                return GetHeadMeshSet;
            }


            public static void Postfix(ref GraphicMeshSet __result, Pawn pawn)
            {
                __result = GetBodyOverlayMeshForPawn(__result, pawn);
            }
        }
    }
}