namespace EdsDcfNet.Models;

/// <summary>
/// Represents the [DynamicChannels] section describing dynamic network variable segments
/// for CiA 302-4 programmable devices.
/// </summary>
public class DynamicChannels
{
    /// <summary>
    /// List of dynamic channel segments.
    /// </summary>
    public List<DynamicChannelSegment> Segments { get; set; } = new();
}

/// <summary>
/// Represents a single segment within the [DynamicChannels] section.
/// </summary>
public class DynamicChannelSegment
{
    /// <summary>
    /// Data type index for this segment (Unsigned16).
    /// </summary>
    public ushort Type { get; set; }

    /// <summary>
    /// Direction/access type for this segment (e.g. ro, rww, rwr).
    /// </summary>
    public AccessType Dir { get; set; }

    /// <summary>
    /// Index range for this segment (e.g. "0xA080-0xA0BF").
    /// </summary>
    public string Range { get; set; } = string.Empty;

    /// <summary>
    /// Process image offset for this segment (Unsigned32).
    /// </summary>
    public uint PPOffset { get; set; }
}
