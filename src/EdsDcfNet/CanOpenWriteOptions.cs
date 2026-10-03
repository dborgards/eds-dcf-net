namespace EdsDcfNet;

using System.Text;

/// <summary>
/// Optional behavior for <see cref="CanOpenFile"/> and format-specific operations
/// (<see cref="CanOpenFile.Eds"/>, <see cref="CanOpenFile.Dcf"/>, <see cref="CanOpenFile.Cpj"/>,
/// <see cref="CanOpenFile.Xdd"/>, <see cref="CanOpenFile.Xdc"/>) write methods.
/// </summary>
/// <remarks>
/// This type intentionally holds only cross-format write concerns. Format-specific
/// options are introduced as derived per-format option types (unsealing this type
/// on demand) rather than as additional properties here — see the
/// "Options extension pattern" section in the README.
/// </remarks>
public sealed class CanOpenWriteOptions
{
    /// <summary>
    /// Gets a default options instance with validation disabled.
    /// </summary>
    public static CanOpenWriteOptions Default { get; } = new();

    /// <summary>
    /// Gets an options instance that validates the model before writing.
    /// </summary>
    public static CanOpenWriteOptions Validated { get; } = new() { ValidateBeforeWrite = true };

    /// <summary>
    /// When <see langword="true"/>, write methods validate the model and throw
    /// <see cref="Exceptions.ModelValidationException"/> when validation issues are found.
    /// Default is <see langword="false"/> for backward compatibility.
    /// </summary>
    public bool ValidateBeforeWrite { get; init; }

    /// <summary>
    /// Gets the encoding used for byte output of every format (EDS, DCF, CPJ, XDD, and XDC).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> (the default) writes UTF-8 without a byte-order mark, as previous
    /// releases did. File and stream output, synchronous and asynchronous, uses this encoding.
    /// For XDD and XDC the XML declaration names the same encoding.
    /// </para>
    /// <para>
    /// <c>WriteToString</c> returns a .NET string. This property affects only byte output;
    /// the string itself is unchanged, and the XDD/XDC declaration in that string stays UTF-8.
    /// </para>
    /// <para>
    /// Characters the encoding cannot represent are not replaced with a substitute character.
    /// EDS, DCF, and CPJ throw the format's write exception; the inner
    /// <see cref="EncoderFallbackException"/> names the character and its index in the output.
    /// The destination file is left unchanged. XDD and XDC emit a numeric character reference
    /// for those characters in text and attribute values.
    /// </para>
    /// </remarks>
    public Encoding? Encoding { get; init; }

    private string? _newLine;

    /// <summary>
    /// Gets the line ending of the written output of every format (EDS, DCF, CPJ, XDD, and XDC).
    /// Only <c>"\n"</c> and <c>"\r\n"</c> are accepted. The default is
    /// <see cref="Environment.NewLine"/>, which keeps the output of previous releases unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Any other value, including <see langword="null"/>, an empty string, a lone <c>"\r"</c>,
    /// and arbitrary text, is rejected with an <see cref="ArgumentException"/> when the property
    /// is set, not when writing. A free-form string would let a caller inject INI sections after
    /// every line or produce XML that is not well-formed.
    /// </para>
    /// <para>
    /// EDS, DCF, and CPJ output contains only the chosen line ending. For XDD and XDC the chosen
    /// value is the line ending of the indentation between elements, and line breaks inside text
    /// content are written as the chosen value too (an XML parser reads either form back as a
    /// line feed). Line breaks inside attribute values stay character references, so they are
    /// preserved exactly. <c>WriteToString</c>, <c>WriteFile</c>, and <c>WriteStream</c>
    /// (synchronous and asynchronous) all apply the option.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The value is neither <c>"\n"</c> nor <c>"\r\n"</c>.</exception>
    public string NewLine
    {
        get => _newLine ?? Environment.NewLine;
        init
        {
            if (value != "\n" && value != "\r\n")
                throw new ArgumentException(
                    "NewLine must be \"\\n\" or \"\\r\\n\".",
                    nameof(value));

            _newLine = value;
        }
    }

    internal static Encoding? ResolveEncoding(CanOpenWriteOptions? options)
        => options?.Encoding;

    /// <summary>The explicitly chosen line ending, or <see langword="null"/> when the default applies.</summary>
    internal static string? ResolveNewLine(CanOpenWriteOptions? options)
        => options?._newLine;
}
