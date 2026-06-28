namespace EdsDcfNet;

/// <summary>
/// CANopen Node-ID range constants and helpers (CiA 306: 1..127).
/// </summary>
internal static class CanOpenNodeId
{
    internal const byte MinValue = 1;
    internal const byte MaxValue = 127;
    internal const string RangeDescription = "1..127";

    internal static bool IsInRange(int nodeId) => nodeId >= MinValue && nodeId <= MaxValue;
}
