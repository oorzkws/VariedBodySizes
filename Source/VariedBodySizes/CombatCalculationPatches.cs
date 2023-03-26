using HarmonyLib;
using RimWorld;
using Verse;

namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    [HarmonyPatch(typeof(VerbProperties), "GetDamageFactorFor", typeof(Tool), typeof(Pawn),
        typeof(HediffComp_VerbGiver))]
    public static class VerbProperties_GetDamageFactorForPatch
    {
        public static void Postfix(ref float result, Pawn attacker, VerbProperties __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectMeleeDamage)
            {
                return;
            }

            if (!__instance.IsMeleeAttack)
            {
                return;
            }

            result *= GetScalarForPawn(attacker);
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetDodgeChance")]
    public static class VerbMeleeAttack_GetDodgeChancePatch
    {
        public static void Postfix(ref float result, LocalTargetInfo target)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectMeleeDodgeChance)
            {
                return;
            }

            if (target.Thing is not Pawn pawn)
            {
                return;
            }

            // Move it towards whichever is advantageous/disadvantageous based on body size
            var new_result = result < 0 ? result * GetScalarForPawn(pawn) : result / GetScalarForPawn(pawn);
            Main.LogMessage($"Dodge chance for {pawn.LabelShort} modified: {result * 100}% -> {new_result * 100}%");
            result = new_result;
        }
    }
}