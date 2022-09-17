using HarmonyLib;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(VerbProperties), "GetDamageFactorFor", typeof(Tool), typeof(Pawn), typeof(HediffComp_VerbGiver))]
public static class VerbProperties_GetDamageFactorFor
{
    public static void Postfix(ref float __result, Pawn attacker, VerbProperties __instance)
    {
        if (!__instance.IsMeleeAttack)
        {
            return;
        }

        if (attacker == null)
        {
            return;
        }

        if (!VariedBodySizesMod.instance.Settings.AffectMeleeDamage)
        {
            return;
        }

        if (Main.CurrentComponent == null)
        {
            return;
        }

        __result *= Main.CurrentComponent.GetVariedBodySize(attacker);
    }
}