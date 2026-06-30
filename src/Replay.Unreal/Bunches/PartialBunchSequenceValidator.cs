using Replay.Models.Net;

namespace Replay.Unreal.Bunches;

internal enum PartialBunchSequenceError
{
    None,
    OverlappingInitial,
    MissingInitial,
    MismatchedContinuation,
}

internal static class PartialBunchSequenceValidator
{
    public static PartialBunchSequenceError ValidateInitial(bool hasExistingState, bool existingIsComplete) =>
        hasExistingState && !existingIsComplete
            ? PartialBunchSequenceError.OverlappingInitial
            : PartialBunchSequenceError.None;

    public static PartialBunchSequenceError ValidateContinuation(
        bool hasExistingState,
        bool existingIsComplete,
        int previousSequence,
        bool previousReliable,
        RawBunchHeader header)
    {
        if (!hasExistingState || existingIsComplete)
        {
            return PartialBunchSequenceError.MissingInitial;
        }

        if (previousReliable != header.bReliable || !SequenceMatches(previousSequence, previousReliable, header.ChSequence))
        {
            return PartialBunchSequenceError.MismatchedContinuation;
        }

        return PartialBunchSequenceError.None;
    }

    private static bool SequenceMatches(int previousSequence, bool reliable, int currentSequence) =>
        reliable
            ? currentSequence == previousSequence + 1
            : currentSequence == previousSequence + 1 || currentSequence == previousSequence;
}