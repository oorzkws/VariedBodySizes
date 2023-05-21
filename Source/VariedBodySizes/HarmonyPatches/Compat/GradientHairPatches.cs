using System.Reflection.Emit;

namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    [HarmonyPatch]
    public static class GradientHair_MultiMaskInitPatch
    {
        private static readonly MethodBase maskInit =
            AccessTools.Method("GradientHair.Graphic_MultiMask:Init");

        public static bool Prepare()
        {
            return ModsConfig.IsActive("automatic.gradienthair") && NotNull(maskInit);
        }

        public static MethodBase TargetMethod()
        {
            return maskInit;
        }

        // The input path in the originating function is processed as path.Split('\0') where [1] is the mask path, if present
        // Otherwise the graphic should fall back to req.maskPath
        // ReSharper disable once SuggestBaseTypeForParameter -- we want to match the signature as closely as possible
        private static string ProcessMaskPath(string[] paths, GraphicRequest req)
        {
            return paths.Length > 1 ? paths[1] : req.maskPath;
        }

        // This patch is basically implementing https://github.com/AUTOMATIC1111/GradientHair/pull/3/files from our side
        public static CodeInstructions Transpiler(CodeInstructions instructions)
        {
            var editor = new CodeMatcher(instructions);
            // Replace the local that holds the mask path with `self.maskPath = args.Length > 1 ? args[1] : req.maskPath;`
            // ReSharper disable all UnusedParameter.Local
            var pattern = InstructionMatchSignature((Graphic self, GraphicRequest req) =>
            {
                var args = req.path.Split('\0');
                var mask = args[1];
            });
            var replacement = InstructionSignature((Graphic self, GraphicRequest req) =>
            {
                var args = req.path.Split('\0');
                self.maskPath = ProcessMaskPath(args, req);
            });
            editor.Replace(pattern, replacement);

            // Replace lookups for the local with the field reference
            // e.g. `array2[0] = ContentFinder<Texture2D>.Get(mask, false);` -> `array2[0] = ContentFinder<Texture2D>.Get(maskPath, false);`
            var lookupPattern = new[] { new CodeMatch(OpCodes.Ldloc_1) };
            var lookupReplacement = InstructionSignature((Graphic self) => self.maskPath).ToArray();
            while (editor.IsValid)
            {
                editor.Replace(lookupPattern, lookupReplacement, suppress: true);
            }

            // Done!
            return editor.InstructionEnumeration();
        }
    }
}