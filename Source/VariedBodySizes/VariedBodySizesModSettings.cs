using System.Collections.Generic;
using Verse;

namespace VariedBodySizes;

/// <summary>
///     Definition of the settings for the mod
/// </summary>
internal class VariedBodySizesModSettings : ModSettings
{
    public bool AffectMeleeDamage;
    public bool AffectRealBodySize;
    public bool AffectRealHealthScale;
    public FloatRange DefaultVariation = new FloatRange(0.9f, 1.1f);
    public Dictionary<string, FloatRange> VariedBodySizes;
    private List<string> variedBodySizesKeys;

    private List<FloatRange> variedBodySizesValues;
    public bool VerboseLogging;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref VerboseLogging, "VerboseLogging");
        Scribe_Values.Look(ref AffectRealBodySize, "AffectRealBodySize");
        Scribe_Values.Look(ref AffectRealHealthScale, "AffectRealHealthScale");
        Scribe_Values.Look(ref AffectMeleeDamage, "AffectMeleeDamage");
        Scribe_Values.Look(ref DefaultVariation, "DefaultVariation", new FloatRange(0.9f, 1.1f));
        Scribe_Collections.Look(ref VariedBodySizes, "VariedBodySizes", LookMode.Value,
            LookMode.Value,
            ref variedBodySizesKeys, ref variedBodySizesValues);
    }

    public void ResetSettings()
    {
        VariedBodySizes = new Dictionary<string, FloatRange>();
        DefaultVariation = new FloatRange(0.9f, 1.1f);
    }
}