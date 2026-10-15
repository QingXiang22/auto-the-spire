namespace BaseSpire.Slice.Compilation;

// Only allocates IR local names during compilation; it is never runtime game state.
internal sealed class CompilationSlots
{
    private int next;
    internal int Next() => next++;
}
