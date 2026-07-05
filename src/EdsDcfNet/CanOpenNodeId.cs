namespace EdsDcfNet;

/// <summary>
/// CANopen Node-ID range constants and helpers (CiA 306: 1..127).
/// </summary>
/// <remarks>
/// Validation error messages produced by <see cref="CanOpenFile.Validate(Models.DeviceConfigurationFile)"/>
/// reference this range via <see cref="RangeDescription"/>.
/// </remarks>
public static class CanOpenNodeId
{
    /// <summary>
    /// The smallest valid CANopen Node-ID (1).
    /// </summary>
    public const byte MinValue = 1;

    /// <summary>
    /// The largest valid CANopen Node-ID (127).
    /// </summary>
    public const byte MaxValue = 127;

    /// <summary>
    /// Human-readable description of the valid Node-ID range ("1..127"),
    /// as used in validation error messages.
    /// </summary>
    public const string RangeDescription = "1..127";

    /// <summary>
    /// Returns whether <paramref name="nodeId"/> is a valid CANopen Node-ID (1..127).
    /// </summary>
    /// <param name="nodeId">The node ID to check.</param>
    /// <returns><see langword="true"/> if the value is within 1..127; otherwise <see langword="false"/>.</returns>
    public static bool IsInRange(int nodeId) => nodeId >= MinValue && nodeId <= MaxValue;
}
