using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace VariedBodySizes;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static partial class HarmonyPatches
{
    [HarmonyPatch(typeof(VerbProperties), "GetDamageFactorFor", typeof(Tool), typeof(Pawn),
        typeof(HediffComp_VerbGiver))]
    [UsedImplicitly]
    public static class VerbProperties_GetDamageFactorForPatch
    {
        public static float Postfix(float result, Pawn attacker, VerbProperties __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectMeleeDamage) return result;
            if (!__instance.IsMeleeAttack) return result;

            return result * GetScalarForPawn(attacker);
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetDodgeChance")]
    [UsedImplicitly]
    public static class VerbMeleeAttack_GetDodgeChancePatch
    {
        [UsedImplicitly]
        public static float Postfix(float result, LocalTargetInfo target)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectMeleeDodgeChance) return result;
            if(target.Thing is not Pawn pawn) return result;

            var new_result = result / GetScalarForPawn(pawn);
            Main.LogMessage($"Dodge chance for {pawn.LabelShort} modified: {result * 100}% -> {new_result * 100}%");
            return new_result;
        }
    }
}