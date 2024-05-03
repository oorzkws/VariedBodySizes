using LudeonTK;

namespace VariedBodySizes;

public static class DebugChangePawnSize
{
    private static bool ShiftIsHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    [DebugAction("Pawn size", "Size +1% (Shift +10%)", actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Pawns_IncreasePawnSize(Pawn pawn)
    {
        changeSize(pawn, true);
    }

    [DebugAction("Pawn size", "Reset size to default", actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Pawns_ResetPawnSize(Pawn pawn)
    {
        changeSize(pawn, reset: true);
    }

    [DebugAction("Pawn size", "Size -1% (Shift -10%)", actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Pawns_DecreasePawnSize(Pawn pawn)
    {
        changeSize(pawn, decrease: true);
    }

    [DebugAction("Pawn size", "Random in set range", actionType = DebugActionType.ToolMapForPawns,
        allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void Pawns_RandomPawnSize(Pawn pawn)
    {
        changeSize(pawn, random: true);
    }

    private static void changeSize(Pawn pawn, bool increase = false, bool decrease = false, bool reset = false,
        bool random = false)
    {
        var currentSize = Main.CurrentComponent.GetVariedBodySize(pawn);
        var message = "";
        var percentChange = 0.01f;
        if (ShiftIsHeld)
        {
            percentChange = 0.1f;
        }

        if (increase)
        {
            currentSize += percentChange;
            message =
                $"Size of {pawn.Name} increased by {percentChange.ToStringPercent()} to {currentSize.ToStringPercent()}";
            if (currentSize > VariedBodySizesMod.MaximumSize)
            {
                currentSize = VariedBodySizesMod.MaximumSize;
                message =
                    $"Size of {pawn.Name} increased by {percentChange.ToStringPercent()} but was limited to the maximum value of {currentSize.ToStringPercent()}";
            }
        }

        if (decrease)
        {
            currentSize -= percentChange;
            message =
                $"Size of {pawn.Name} decreased by -{percentChange.ToStringPercent()} to {currentSize.ToStringPercent()}";
            if (currentSize < VariedBodySizesMod.MinimumSize)
            {
                currentSize = VariedBodySizesMod.MinimumSize;
                message =
                    $"Size of {pawn.Name} decreased by {percentChange.ToStringPercent()} but was limited to the minimum value of {currentSize.ToStringPercent()}";
            }
        }

        if (reset)
        {
            currentSize = 1f;
            message =
                $"Size of {pawn.Name} reset to {currentSize.ToStringPercent()}";
        }

        if (random)
        {
            currentSize = Main.GetPawnVariation(pawn);
            message =
                $"Size of {pawn.Name} randomized in the set range to {currentSize.ToStringPercent()}";
        }

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        Main.CurrentComponent.VariedBodySizesDictionary[pawn.thingIDNumber] = currentSize;
        Main.ResetAllCaches(pawn);
        // Required for...reasons? The game doesn't seem to update its internal cache until the game is unpaused
        // So we have to update our own value instead of invalidating it and asking the game
        Main.CurrentComponent.sizeCache.Set(pawn, currentSize);
        Messages.Message(message, MessageTypeDefOf.TaskCompletion, false);
        DebugActionsUtility.DustPuffFrom(pawn);
    }
}