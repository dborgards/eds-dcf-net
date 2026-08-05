namespace EdsDcfNet.Models;

/// <summary>
/// CiA 311 <c>PDOmapping</c> attribute values for XDD/XDC objects.
/// </summary>
/// <remarks>
/// EDS/DCF only distinguish mappable vs not (<c>0</c>/<c>1</c>). When reading EDS/DCF,
/// <see cref="Optional"/> is used for mappable entries. XDD/XDC preserve the full token.
/// </remarks>
public enum PdoMappingMode
{
    /// <summary>Not PDO-mappable (<c>no</c>).</summary>
    No = 0,

    /// <summary>Mapped by default (<c>default</c>).</summary>
    Default = 1,

    /// <summary>Optionally mappable (<c>optional</c>).</summary>
    Optional = 2,

    /// <summary>TPDO-only mapping (<c>TPDO</c>).</summary>
    Tpdo = 3,

    /// <summary>RPDO-only mapping (<c>RPDO</c>).</summary>
    Rpdo = 4
}
