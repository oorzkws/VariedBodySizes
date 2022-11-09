using HarmonyLib;
using RimWorld;
using Verse;

namespace VariedBodySizes;

[HarmonyPatch(typeof(Verb_MeleeAttack), "GetDodgeChance")]
public static class VerbMeleeAttack_GetDodgeChance
{
    public static void Postfix(ref float __result, LocalTargetInfo target)
    {
        if (!VariedBodySizesMod.instance.Settings.AffectMeleeDodgeChance)
        {
            return;
        }

        if (target.Thing is not Pawn pawn)
        {
            return;
        }

        __result /= Main.CurrentComponent.GetVariedBodySize(pawn);
    }
}