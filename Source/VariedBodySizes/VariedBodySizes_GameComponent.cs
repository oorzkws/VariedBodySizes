using System.Collections.Generic;
using Verse;

namespace VariedBodySizes;

public class VariedBodySizes_GameComponent : GameComponent
{
    public Dictionary<Pawn, float> VariedBodySizesDictionary;
    
    private List<Pawn> variedBodySizesDictionaryKeys;
    private List<float> variedBodySizesDictionaryValues;

    public VariedBodySizes_GameComponent(Game game)
    {
        Main.CurrentComponent = this;
    }

    public float GetVariedBodySize(Pawn pawn)
    {
        if (pawn == null)
        {
            return 1f;
        }

        if (Scribe.mode != LoadSaveMode.Inactive)
        {
            return 1f;
        }

        VariedBodySizesDictionary ??= new Dictionary<Pawn, float>();

        if (VariedBodySizesDictionary.ContainsKey(pawn))
        {
            return VariedBodySizesDictionary[pawn];
        }

        VariedBodySizesDictionary[pawn] = Main.GetPawnVariation(pawn);
        Main.LogMessage($"[VariedBodySizes]: Setting size of {pawn} to {VariedBodySizesDictionary[pawn]}");

        return VariedBodySizesDictionary[pawn];
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref VariedBodySizesDictionary, "VariedBodySizesDictionary", LookMode.Reference,
            LookMode.Value,
            ref variedBodySizesDictionaryKeys, ref variedBodySizesDictionaryValues, false);
    }
}