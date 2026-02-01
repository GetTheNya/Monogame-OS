using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace TheGame.Core.OS;

/// <summary>
/// A TextWriter that redirects output to a terminal's line buffer.
/// </summary>
public class TerminalWriter : TextWriter {
    private readonly Action<string, Color, string> _onWrite;
    private readonly Color _defaultColor;
    private readonly string _source;
    private StringBuilder _currentLine = new();

    public override Encoding Encoding => Encoding.UTF8;

    public TerminalWriter(Action<string, Color, string> onWrite, Color defaultColor, string source) {
        _onWrite = onWrite;
        _defaultColor = defaultColor;
        _source = source;
    }

    public override void Write(char value) {
        if (value == '\n') {
            FlushLine();
        } else if (value != '\r') {
            _currentLine.Append(value);
            // Auto-flush partial lines if they look like prompts (end with space or colon or >)
            if (value == ' ' || value == ':' || value == '>') {
                Flush();
            }
        }
    }

    public override void Write(string value) {
        if (string.IsNullOrEmpty(value)) return;
        base.Write(value);
        if (!value.EndsWith("\n")) Flush();
    }

    private void FlushLine() {
        _onWrite?.Invoke(_currentLine.ToString() + "\n", _defaultColor, _source);
        _currentLine.Clear();
    }

    public override void Flush() {
        if (_currentLine.Length > 0) {
            _onWrite?.Invoke(_currentLine.ToString(), _defaultColor, _source);
            _currentLine.Clear();
        }
    }

    protected override void Dispose(bool disposing) {
        Flush();
        base.Dispose(disposing);
    }
}

/// <summary>
/// A TextReader that reads from a thread-safe queue populated by the terminal UI.
/// Supports async cooperative reads.
/// </summary>
public class TerminalReader : TextReader {
    private readonly ConcurrentQueue<string> _inputQueue = new();
    private readonly ConcurrentQueue<TaskCompletionSource<string>> _waitingReaders = new();
    private string _currentLine = null;
    private int _charIndex = 0;

    public void EnqueueInput(string text) {
        if (_waitingReaders.TryDequeue(out var tcs)) {
            tcs.TrySetResult(text);
        } else {
            _inputQueue.Enqueue(text);
        }
    }

    public void ClearInput() {
        while (_inputQueue.TryDequeue(out _));
        while (_waitingReaders.TryDequeue(out var tcs)) tcs.TrySetCanceled();
    }

    public override string ReadLine() {
        if (_inputQueue.TryDequeue(out string line)) {
            return line;
        }
        
        // No more blocking wait here. Threading is removed.
        return null; 
    }

    public override async Task<string> ReadLineAsync() {
        if (_inputQueue.TryDequeue(out string line)) {
            return line;
        }

        var tcs = new TaskCompletionSource<string>();
        _waitingReaders.Enqueue(tcs);
        return await tcs.Task;
    }

    public override int Read() {
        if (_currentLine == null || _charIndex >= _currentLine.Length) {
            string next = ReadLine();
            if (next == null) return -1;
            _currentLine = next + "\n";
            _charIndex = 0;
        }
        return _currentLine[_charIndex++];
    }

    public async Task<int> ReadAsync() {
        if (_currentLine == null || _charIndex >= _currentLine.Length) {
            string next = await ReadLineAsync();
            if (next == null) return -1;
            _currentLine = next + "\n";
            _charIndex = 0;
        }
        return _currentLine[_charIndex++];
    }

    public override int Peek() {
        if (_currentLine == null || _charIndex >= _currentLine.Length) {
            if (!_inputQueue.TryPeek(out string nextLine)) {
                return -1;
            }
            return nextLine.Length > 0 ? nextLine[0] : '\n';
        }

        return _currentLine[_charIndex];
    }
}

/// <summary>
/// A TextWriter that writes directly to the Virtual File System.
/// </summary>
public class VfsWriter : TextWriter {
    private readonly string _path;
    private readonly bool _append;
    private StringBuilder _buffer = new();

    public override Encoding Encoding => Encoding.UTF8;

    public VfsWriter(string path, bool append = false) {
        _path = path;
        _append = append;
    }

    public override void Write(char value) {
        _buffer.Append(value);
    }

    public override void Flush() {
        if (_buffer.Length == 0) return;
        
        string content = _buffer.ToString();
        _buffer.Clear();

        if (_append && VirtualFileSystem.Instance.Exists(_path)) {
            string existing = VirtualFileSystem.Instance.ReadAllText(_path);
            VirtualFileSystem.Instance.WriteAllText(_path, existing + content);
        } else {
            VirtualFileSystem.Instance.WriteAllText(_path, content);
        }
    }

    protected override void Dispose(bool disposing) {
        Flush();
        base.Dispose(disposing);
    }
}

/// <summary>
/// A bridge that connects a TextWriter to a TextReader for piping operations.
/// </summary>
public class PipeBridge {
    private readonly TerminalReader _reader = new();
    
    public TextWriter Writer { get; }
    public TextReader Reader => _reader;

    public PipeBridge() {
        Writer = new TerminalWriter((text, color, source) => _reader.EnqueueInput(text), Color.White, "PIPE");
    }
}
