using HarmonyLib;
using RimWorld;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(VerbProperties), "GetDamageFactorFor", typeof(Tool), typeof(Pawn), typeof(HediffComp_VerbGiver))]
public static class VerbProperties_GetDamageFactorForPatch
{
    public static void Postfix(ref float __result, Pawn attacker, VerbProperties __instance)
    {
        if (!VariedBodySizesMod.instance.Settings.AffectMeleeDamage ||
            !__instance.IsMeleeAttack ||
            attacker == null ||
            Main.CurrentComponent == null
        ){
            return;
        }

        __result *= Main.CurrentComponent.GetVariedBodySize(attacker);
    }
}

[HarmonyPatch(typeof(Verb_MeleeAttack), "GetDodgeChance")]
public static class VerbMeleeAttack_GetDodgeChancePatch
{
    private static readonly TimedCache<float> statCache = new(360);
    public static void Postfix(ref float __result, LocalTargetInfo target)
    {
        if (!VariedBodySizesMod.instance.Settings.AffectMeleeDodgeChance || target.Thing is not Pawn pawn)
            return;

        __result /= Main.CurrentComponent.GetVariedBodySize(pawn);
    }
}