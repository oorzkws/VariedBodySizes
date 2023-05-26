using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace VariedBodySizes;

public class VariedBodySizes_GameComponent : GameComponent
{
    internal readonly TimedCache<float> sizeCache = new TimedCache<float>(36);
    public Dictionary<int, float> VariedBodySizesDictionary;

    // ReSharper disable once UnusedParameter.Local
    public VariedBodySizes_GameComponent(Game game)
    {
        Main.CurrentComponent = this;
    }

    // This way others can hook and modify while benefiting from our cache
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.PreserveSig)]
    public static float OnCalculateBodySize(float bodySize, Pawn pawn)
    {
        return pawn == null ? 1f : bodySize;
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

        // cached value, or calculate, cache and return
        if (sizeCache.TryGet(pawn, out var cachedSize))
        {
            return cachedSize;
        }

        VariedBodySizesDictionary ??= new Dictionary<int, float>();

        var pawnId = pawn.thingIDNumber;
        if (!VariedBodySizesDictionary.TryGetValue(pawnId, out var bodySize))
        {
            bodySize = Main.GetPawnVariation(pawn);
            VariedBodySizesDictionary[pawnId] = bodySize;

            // Only delegate the string building when it's relevant
            if (VariedBodySizesMod.instance.Settings.VerboseLogging)
            {
                Main.LogMessage($"Setting size of {pawn.NameFullColored} ({pawn.ThingID}) to {bodySize}");
            }
        }

        // Apply any registered modifiers when storing
        bodySize = OnCalculateBodySize(bodySize, pawn);
        sizeCache.Set(pawn, bodySize);

        return bodySize;
    }

    /// <summary>
    ///     Load the body size dict from XML and convert it from &lt;Pawn, float&gt; to &lt;int, float&gt;
    /// </summary>
    /// <returns>bool - whether the dictionary was migrated</returns>
    private bool MigrateDictionaryFormat()
    {
        var dictPath =
            "/savegame/game/components/li[@Class=\"VariedBodySizes.VariedBodySizes_GameComponent\"]/VariedBodySizesDictionary";
        var document = Scribe.loader.curXmlParent?.OwnerDocument;
        var bodySizeDict = document?.SelectSingleNode(dictPath);
        var keys = bodySizeDict?["keys"]?.ChildNodes;
        var values = bodySizeDict?["values"]?.ChildNodes;

        if (bodySizeDict is null || keys is null || values is null)
        {
            return false;
        }

        var i = 0;
        while (true)
        {
            VariedBodySizesDictionary ??= new Dictionary<int, float>();
            var key = keys[i];
            var value = values[i++]; // next iter here
            if (key is null || value is null)
            {
                break;
            }

            var keyText =
                Regex.Match(key.InnerText, @"\d+$").Value; // drop all non-number, format is usually Thing_Human1234
            var hasKey = int.TryParse(keyText, out var keyInt);
            var hasValue = float.TryParse(value.InnerText, out var valueFloat);
            if (hasKey && hasValue)
            {
                VariedBodySizesDictionary.Add(keyInt, valueFloat);
            }
        }

        //bodySizeDict.ParentNode?.RemoveChild(bodySizeDict);

        return VariedBodySizesDictionary is not null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        // If it's migrated, we're initialized and all that. Don't have to care further.
        if (Scribe.mode == LoadSaveMode.LoadingVars && MigrateDictionaryFormat())
        {
            return;
        }

        Scribe_Collections.Look(ref VariedBodySizesDictionary, "VariedBodySizesData", LookMode.Value, LookMode.Value);
        // If we don't have one saved, make it
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            VariedBodySizesDictionary ??= new Dictionary<int, float>();
        }
    }
}