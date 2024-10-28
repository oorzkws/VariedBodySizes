namespace VariedBodySizes;

public static partial class HarmonyPatches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.BodySize), MethodType.Getter)]
    public static class Pawn_BodySizePatch
    {
        public static void Postfix(ref float __result, Pawn __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealBodySize)
            {
                return;
            }

            // The game can call stat lookups on pawns that aren't finished generating, if we cache this we shoot ourselves in the foot
            // For some reason this always ends up as  __result == baseBodySize * 0.2f
            if (!__instance.Dead && __instance.needs is null)
            {
                return;
            }

            __result *= GetScalarForPawn(__instance);

            // Babies won't fit in cribs if body size exceeds 0.25
            if (__instance.DevelopmentalStage == DevelopmentalStage.Baby)
            {
                __result = Mathf.Min(__result, VariedBodySizesMod.MinimumSize);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.HealthScale), MethodType.Getter)]
    public static class Pawn_HealthScalePatch
    {
        public static void Postfix(ref float __result, Pawn __instance)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealHealthScale)
            {
                return;
            }

            __result *= GetScalarForPawn(__instance);
        }
    }

    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.FoodFallPerTickAssumingCategory))]
    public static class Need_Food_FoodFallPerTickAssumingCategoryPatch
    {
        public static void Postfix(ref float __result, Pawn ___pawn)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealHungerRate)
            {
                return;
            }

            __result *= GetScalarForPawn(___pawn);
        }
    }

    [HarmonyPatch(typeof(StatPart_BodySize), nameof(StatPart_BodySize.ExplanationPart))]
    public static class StatPart_BodySize_ExplanationPartPatch
    {
        public static void Postfix(ref string __result, StatRequest req)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectRealBodySize)
            {
                return;
            }

            if (!req.HasThing)
            {
                return;
            }

            if (req.Thing is not Pawn pawn)
            {
                return;
            }

            var scalarValue = GetScalarForPawn(pawn);

            var bodySizeFactor = "VariedBodySizes.sizeNote".Translate(scalarValue.ToStringPercent());

            if (__result == null)
            {
                __result = bodySizeFactor;
                return;
            }

            __result += Environment.NewLine + bodySizeFactor;
        }
    }

    [HarmonyPatch(typeof(RaceProperties), nameof(RaceProperties.NutritionEatenPerDayExplanation))]
    public static class RaceProperties_NutritionEatenPerDayExplanationPatch
    {
        public static void Prefix(Pawn p, out Tuple<float, float> __state)
        {
            __state = new Tuple<float, float>(float.NaN, float.NaN);
            if (!VariedBodySizesMod.instance.Settings.AffectRealHungerRate)
            {
                return;
            }

            var hungerRate = p.def.race.baseHungerRate;
            var scalarValue = GetScalarForPawn(p);
            __state = new Tuple<float, float>(hungerRate, scalarValue);
            p.def.race.baseHungerRate *= scalarValue;
        }


        public static void Postfix(Pawn p, Tuple<float, float> __state, ref string __result)
        {
            if (float.IsNaN(__state.Item1))
            {
                return;
            }

            var indexOfPlaceToAddExplanation = __result.LastIndexOf(Environment.NewLine, StringComparison.Ordinal) - 1;

            __result = __result.Insert(indexOfPlaceToAddExplanation, Environment.NewLine + Environment.NewLine +
                                                                     "VariedBodySizes.sizeChange".Translate() +
                                                                     ": x" + __state.Item2.ToStringPercent() +
                                                                     Environment.NewLine);

            p.def.race.baseHungerRate = __state.Item1;
        }
    }

    [HarmonyPatch]
    public static class CompHasGatherableBodyResource_ResourceAmountPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var subClassType in typeof(CompHasGatherableBodyResource).AllSubclassesNonAbstract())
            {
                yield return AccessTools.PropertyGetter(subClassType, "ResourceAmount");
            }
        }

        public static void Postfix(ref int __result, ThingWithComps ___parent)
        {
            if (!VariedBodySizesMod.instance.Settings.AffectHarvestYield)
            {
                return;
            }

            if (___parent is not Pawn pawn)
            {
                return;
            }

            __result = (int)Math.Round(__result * GetScalarForPawn(pawn));
        }
    }

    [HarmonyPatch(typeof(HediffComp_Chargeable), nameof(HediffComp_Chargeable.GreedyConsume))]
    public static class HediffComp_Chargeable_GreedyConsumePatch
    {
        public static void Prefix(HediffComp_Chargeable __instance, ref float desiredCharge, out bool __state)
        {
            __state = false;
            if (!VariedBodySizesMod.instance.Settings.AffectLactating)
            {
                return;
            }

            if (__instance is not HediffComp_Lactating lactatingComp)
            {
                return;
            }

            __state = true;
            desiredCharge /= GetScalarForPawn(lactatingComp.parent.pawn);
        }

        public static void Postfix(HediffComp_Chargeable __instance, bool __state, ref float __result)
        {
            if (!__state)
            {
                return;
            }

            __result *= GetScalarForPawn(__instance.parent.pawn);
        }
    }
}