namespace pitTeam.Compat
{
    internal static class Spt5BeingHitActionAbiGate
    {
        internal static bool SupportsRawByReferenceValue(bool windowsX64, bool nativeValueType, uint nativeSize) =>
            windowsX64 && nativeValueType && nativeSize > 8;
    }
}