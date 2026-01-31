using System;
using System.IO;

namespace TheGame.Core.OS;

/// <summary>
/// Represents the standard input, output, and error streams for a process.
/// </summary>
public class StandardIO : IDisposable {
    /// <summary> The standard input stream (reading from keyboard or pipe). </summary>
    public TextReader In { get; set; } = TextReader.Null;

    /// <summary> The standard output stream (writing to console or pipe). </summary>
    public TextWriter Out { get; set; } = TextWriter.Null;

    /// <summary> The standard error stream (writing error messages to console or pipe). </summary>
    public TextWriter Error { get; set; } = TextWriter.Null;

    /// <summary>
    /// Disposes the standard I/O streams if they are disposable and not the default Null streams.
    /// </summary>
    public void Dispose() {
        if (In != TextReader.Null) In?.Dispose();
        if (Out != TextWriter.Null) Out?.Dispose();
        if (Error != TextWriter.Null) Error?.Dispose();
        
        In = TextReader.Null;
        Out = TextWriter.Null;
        Error = TextWriter.Null;
    }
}
