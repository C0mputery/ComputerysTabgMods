using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace ComputeryTabgCLI;

/// <summary>
///     Memory-efficient log view that stores logs to a file and reads on-demand.
///     Only keeps line offsets in memory for seeking.
/// </summary>
public class LogView : View {
    private readonly string _logFilePath;
    private readonly string _indexFilePath;
    private FileStream? _logStream;
    private FileStream? _indexStream;
    private BinaryWriter? _logWriter;
    private BinaryWriter? _indexWriter;
    private readonly Lock _lock = new Lock();
    
    // Pending log buffer for thread-safe additions
    private readonly Queue<string> _pendingLogs = new Queue<string>();
    private readonly Lock _pendingLock = new Lock();
    private const int MaxPendingLogs = 1000;
    
    // Line index cache - stores file offset for each line
    // Each entry is 8 bytes (long offset) in the index file
    private int _lineCount;
    private long _currentOffset;
    
    private int _maxLines = 10000;
    private int _topWrappedLine; // Absolute position of top visible wrapped line
    private bool _wordWrap = true;
    private long _droppedLineCount;
    
    // Cache for wrapped line count calculation
    private int _cachedWrappedLineCount;
    private int _cachedWidth = -1;
    private bool _cacheValid;
    
    // Small LRU cache for recently accessed lines during rendering
    private readonly Dictionary<int, string> _lineCache = new Dictionary<int, string>();
    private readonly Queue<int> _lineCacheOrder = new Queue<int>();
    private const int LineCacheSize = 200;
    
    private bool _disposed;
    private bool _needsFlush;
    
    public LogView() : this(Path.Combine(Path.GetTempPath(), $"logview_{Guid.NewGuid():N}")) { }
    
    public LogView(string baseFilePath) {
        _logFilePath = baseFilePath + ".log";
        _indexFilePath = baseFilePath + ".idx";
        
        InitializeFiles();
        
        CanFocus = true;
        BorderStyle = LineStyle.Rounded;
        
        KeyBindings.Add(Key.PageUp, Command.PageUp);
        KeyBindings.Add(Key.PageDown, Command.PageDown);
        KeyBindings.Add(Key.Home, Command.Start);
        KeyBindings.Add(Key.End, Command.End);
        KeyBindings.Add(Key.CursorUp, Command.Up);
        KeyBindings.Add(Key.CursorDown, Command.Down);
        
        AddCommand(
            Command.PageUp, () => {
                PageUp();
                return true;
            }
        );
        AddCommand(
            Command.PageDown, () => {
                PageDown();
                return true;
            }
        );
        AddCommand(
            Command.Start, () => {
                ScrollToTop();
                return true;
            }
        );
        AddCommand(
            Command.End, () => {
                ScrollToBottom();
                return true;
            }
        );
        AddCommand(
            Command.Up, () => {
                ScrollUp();
                return true;
            }
        );
        AddCommand(
            Command.Down, () => {
                ScrollDown();
                return true;
            }
        );
    }
    
    private void InitializeFiles() {
        _logStream = new FileStream(_logFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.RandomAccess);
        _indexStream = new FileStream(_indexFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.RandomAccess);
        _logWriter = new BinaryWriter(_logStream, Encoding.UTF8, true);
        _indexWriter = new BinaryWriter(_indexStream, Encoding.UTF8, true);
        _lineCount = 0;
        _currentOffset = 0;
    }
    
    public int MaxLines {
        get => _maxLines;
        set {
            lock (_lock) {
                _maxLines = Math.Max(100, value);
                TrimOldLinesIfNeeded();
            }
        }
    }
    
    public bool AutoScroll { get; set; } = true;
    
    public bool WordWrap {
        get => _wordWrap;
        set {
            if (_wordWrap != value) {
                _wordWrap = value;
                _cacheValid = false;
                SetNeedsDraw();
            }
        }
    }
    
    public int LineCount {
        get {
            lock (_lock) {
                return _lineCount;
            }
        }
    }
    
    public int WrappedLineCount {
        get {
            lock (_lock) {
                int width = Viewport.Width;
                if (width <= 0) {
                    return _lineCount;
                }
                EnsureWrappedLineCountCache(width);
                return _cachedWrappedLineCount;
            }
        }
    }
    
    public long DroppedLineCount => Interlocked.Read(ref _droppedLineCount);
    
    public int PendingLogCount {
        get {
            lock (_pendingLock) {
                return _pendingLogs.Count;
            }
        }
    }
    
    /// <summary>
    ///     Queue a line to be added (thread-safe).
    /// </summary>
    public void LogLine(string text) {
        lock (_pendingLock) {
            if (_pendingLogs.Count >= MaxPendingLogs) {
                _pendingLogs.Dequeue();
                Interlocked.Increment(ref _droppedLineCount);
            }
            _pendingLogs.Enqueue(text);
        }
    }
    
    /// <summary>
    ///     Flush pending logs to the file. Call from main thread.
    /// </summary>
    public void FlushLogBuffer() {
        string[]? toAdd = null;
        lock (_pendingLock) {
            if (_pendingLogs.Count > 0) {
                toAdd = _pendingLogs.ToArray();
                _pendingLogs.Clear();
            }
        }
        
        if (toAdd != null) {
            AddLines(toAdd);
        }
    }
    
    public void AddLine(string line) {
        lock (_lock) {
            AddLineInternal(line);
            _cacheValid = false;
            SetNeedsDraw();
        }
    }
    
    public void AddLines(IEnumerable<string> lines) {
        lock (_lock) {
            foreach (string line in lines) {
                AddLineInternal(line);
            }
            _cacheValid = false;
            TrimOldLinesIfNeeded();
            SetNeedsDraw();
        }
    }
    
    private void AddLineInternal(string line) {
        if (_logWriter == null || _indexWriter == null || _logStream == null || _indexStream == null) {
            return;
        }
        
        // Ensure we're at the end of the files before writing
        _logStream.Seek(0, SeekOrigin.End);
        _indexStream.Seek(0, SeekOrigin.End);
        _currentOffset = _logStream.Position;
        
        // Write line offset to index
        _indexWriter.Write(_currentOffset);
        
        // Write line to log file (length-prefixed)
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        _logWriter.Write(bytes.Length);
        _logWriter.Write(bytes);
        
        _currentOffset = _logStream.Position;
        _lineCount++;
        _needsFlush = true;
    }
    
    private void TrimOldLinesIfNeeded() {
        if (_lineCount <= _maxLines) {
            return;
        }
        
        // Need to compact the files - this is expensive but rare
        int linesToRemove = _lineCount - _maxLines;
        Interlocked.Add(ref _droppedLineCount, linesToRemove);
        
        CompactFiles(linesToRemove);
    }
    
    private void CompactFiles(int linesToRemove) {
        if (_logStream == null || _indexStream == null) {
            return;
        }
        
        // Flush current writers
        _logWriter?.Flush();
        _indexWriter?.Flush();
        
        string tempLogPath = _logFilePath + ".tmp";
        string tempIndexPath = _indexFilePath + ".tmp";
        
        try {
            using (FileStream tempLogStream = new FileStream(tempLogPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            using (FileStream tempIndexStream = new FileStream(tempIndexPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            using (BinaryWriter tempLogWriter = new BinaryWriter(tempLogStream, Encoding.UTF8, true))
            using (BinaryWriter tempIndexWriter = new BinaryWriter(tempIndexStream, Encoding.UTF8, true)) {
                long newOffset = 0;
                
                // Copy lines after linesToRemove
                for (int i = linesToRemove; i < _lineCount; i++) {
                    string line = ReadLineFromFile(i);
                    
                    tempIndexWriter.Write(newOffset);
                    byte[] bytes = Encoding.UTF8.GetBytes(line);
                    tempLogWriter.Write(bytes.Length);
                    tempLogWriter.Write(bytes);
                    newOffset = tempLogStream.Position;
                }
                
                _currentOffset = newOffset;
            }
            
            // Close current files
            _logWriter?.Dispose();
            _indexWriter?.Dispose();
            _logStream?.Dispose();
            _indexStream?.Dispose();
            
            // Replace files
            File.Delete(_logFilePath);
            File.Delete(_indexFilePath);
            File.Move(tempLogPath, _logFilePath);
            File.Move(tempIndexPath, _indexFilePath);
            
            // Reopen files
            _logStream = new FileStream(_logFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.RandomAccess);
            _indexStream = new FileStream(_indexFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.RandomAccess);
            _logWriter = new BinaryWriter(_logStream, Encoding.UTF8, true);
            _indexWriter = new BinaryWriter(_indexStream, Encoding.UTF8, true);
            
            // Seek to end
            _logStream.Seek(0, SeekOrigin.End);
            _indexStream.Seek(0, SeekOrigin.End);
            
            _lineCount -= linesToRemove;
            _lineCache.Clear();
            _lineCacheOrder.Clear();
            
            // Adjust scroll position - lines were removed from the top
            // We need to figure out how many wrapped lines were removed
            // Since cache is cleared, we can't know exactly, so just clamp
            _topWrappedLine = Math.Max(0, _topWrappedLine);
        }
        catch {
            // If compaction fails, try to clean up temp files
            try { File.Delete(tempLogPath); }
            catch {
                /* Ignore cleanup failures */
            }
            try { File.Delete(tempIndexPath); }
            catch {
                /* Ignore cleanup failures */
            }
        }
    }
    
    private string ReadLineFromFile(int lineIndex) {
        if (_logStream == null || _indexStream == null) {
            return string.Empty;
        }
        if (lineIndex < 0 || lineIndex >= _lineCount) {
            return string.Empty;
        }
        
        // Check cache first
        if (_lineCache.TryGetValue(lineIndex, out string? cached)) {
            return cached;
        }
        
        try {
            // Flush writers if needed to ensure data is on disk
            if (_needsFlush) {
                _logWriter?.Flush();
                _indexWriter?.Flush();
                _needsFlush = false;
            }
            
            // Read offset from index file
            long indexPosition = lineIndex * sizeof(long);
            _indexStream.Seek(indexPosition, SeekOrigin.Begin);
            Span<byte> offsetBytes = stackalloc byte[8];
            if (_indexStream.Read(offsetBytes) != 8) {
                return string.Empty;
            }
            long lineOffset = BitConverter.ToInt64(offsetBytes);
            
            // Read line length from log file
            _logStream.Seek(lineOffset, SeekOrigin.Begin);
            Span<byte> lengthBytes = stackalloc byte[4];
            if (_logStream.Read(lengthBytes) != 4) {
                return string.Empty;
            }
            int length = BitConverter.ToInt32(lengthBytes);
            
            if (length <= 0 || length > 1024 * 1024) {
                return string.Empty; // Sanity check
            }
            
            // Read line content
            byte[] bytes = new byte[length];
            int bytesRead = _logStream.Read(bytes, 0, length);
            if (bytesRead != length) {
                return string.Empty;
            }
            
            string line = Encoding.UTF8.GetString(bytes);
            
            // Add to cache
            AddToLineCache(lineIndex, line);
            
            return line;
        }
        catch {
            return string.Empty;
        }
    }
    
    private void AddToLineCache(int lineIndex, string line) {
        if (_lineCache.ContainsKey(lineIndex)) {
            return;
        }
        
        // Evict oldest if cache is full
        while (_lineCacheOrder.Count >= LineCacheSize) {
            int oldest = _lineCacheOrder.Dequeue();
            _lineCache.Remove(oldest);
        }
        
        _lineCache[lineIndex] = line;
        _lineCacheOrder.Enqueue(lineIndex);
    }
    
    public void Clear() {
        lock (_lock) {
            _logWriter?.Dispose();
            _indexWriter?.Dispose();
            _logStream?.Dispose();
            _indexStream?.Dispose();
            
            InitializeFiles();
            
            _topWrappedLine = 0;
            AutoScroll = true;
            _cacheValid = false;
            _lineCache.Clear();
            _lineCacheOrder.Clear();
            SetNeedsDraw();
        }
    }
    
    private int GetWrappedLineCount(string line, int width) {
        if (string.IsNullOrEmpty(line) || !_wordWrap || line.Length <= width) {
            return 1;
        }
        return (line.Length + width - 1) / width;
    }
    
    private void EnsureWrappedLineCountCache(int width) {
        if (_cacheValid && _cachedWidth == width) {
            return;
        }
        
        int total = 0;
        for (int i = 0; i < _lineCount; i++) {
            string line = ReadLineFromFile(i);
            total += GetWrappedLineCount(line, width);
        }
        
        _cachedWrappedLineCount = total;
        _cachedWidth = width;
        _cacheValid = true;
    }
    
    protected override bool OnDrawingContent(DrawContext? context) {
        base.OnDrawingContent(context);
        
        lock (_lock) {
            int width = Viewport.Width;
            int height = Viewport.Height;
            
            if (height <= 0 || width <= 0 || _lineCount == 0) {
                return true;
            }
            
            EnsureWrappedLineCountCache(width);
            
            int totalWrappedLines = _cachedWrappedLineCount;
            int maxTopLine = Math.Max(0, totalWrappedLines - height);
            
            // If auto-scroll is on, always show the bottom
            if (AutoScroll) {
                _topWrappedLine = maxTopLine;
            }
            else {
                // Clamp to valid range
                _topWrappedLine = Math.Clamp(_topWrappedLine, 0, maxTopLine);
            }
            
            int targetLine = _topWrappedLine;
            
            // Find which source line and sub-line to start from
            int wrappedCount = 0;
            int startLineIndex = 0;
            int startSubLine = 0;
            
            for (int i = 0; i < _lineCount; i++) {
                string line = ReadLineFromFile(i);
                int lineWraps = GetWrappedLineCount(line, width);
                if (wrappedCount + lineWraps > targetLine) {
                    startLineIndex = i;
                    startSubLine = targetLine - wrappedCount;
                    break;
                }
                wrappedCount += lineWraps;
            }
            
            // Render visible lines
            int y = 0;
            for (int i = startLineIndex; i < _lineCount && y < height; i++) {
                string line = ReadLineFromFile(i);
                int lineWraps = GetWrappedLineCount(line, width);
                
                int subStart = i == startLineIndex ? startSubLine : 0;
                
                for (int sub = subStart; sub < lineWraps && y < height; sub++) {
                    Move(0, y);
                    
                    if (string.IsNullOrEmpty(line)) {
                        // Empty line
                    }
                    else if (!_wordWrap || line.Length <= width) {
                        AddStr(line.Length > width ? line[..width] : line);
                    }
                    else {
                        int start = sub * width;
                        int len = Math.Min(width, line.Length - start);
                        if (len > 0) {
                            AddStr(line.Substring(start, len));
                        }
                    }
                    y++;
                }
            }
        }
        
        return true;
    }
    
    public void ScrollUp(int lines = 1) {
        lock (_lock) {
            _topWrappedLine = Math.Max(0, _topWrappedLine - lines);
            AutoScroll = false;
            SetNeedsDraw();
        }
    }
    
    public void ScrollDown(int lines = 1) {
        lock (_lock) {
            int width = Viewport.Width;
            if (width <= 0) {
                return;
            }
            
            EnsureWrappedLineCountCache(width);
            int maxTopLine = Math.Max(0, _cachedWrappedLineCount - Viewport.Height);
            
            _topWrappedLine = Math.Min(_topWrappedLine + lines, maxTopLine);
            
            // Re-enable auto-scroll if we're at the bottom
            if (_topWrappedLine >= maxTopLine) {
                AutoScroll = true;
            }
            
            SetNeedsDraw();
        }
    }
    
    public void PageUp() {
        ScrollUp(Math.Max(1, Viewport.Height - 1));
    }
    
    public void PageDown() {
        ScrollDown(Math.Max(1, Viewport.Height - 1));
    }
    
    public void ScrollToTop() {
        lock (_lock) {
            _topWrappedLine = 0;
            AutoScroll = false;
            SetNeedsDraw();
        }
    }
    
    public void ScrollToBottom() {
        lock (_lock) {
            AutoScroll = true;
            // _topWrappedLine will be updated on next draw
            SetNeedsDraw();
        }
    }
    
    protected override bool OnMouseEvent(MouseEventArgs mouseEvent) {
        if (!mouseEvent.Handled && CanFocus && !HasFocus) {
            SetFocus();
        }
        
        if (mouseEvent.Flags.HasFlag(MouseFlags.WheeledDown)) {
            ScrollDown(3);
            return true;
        }
        
        if (mouseEvent.Flags.HasFlag(MouseFlags.WheeledUp)) {
            ScrollUp(3);
            return true;
        }
        
        return base.OnMouseEvent(mouseEvent);
    }
    
    protected override void Dispose(bool disposing) {
        if (_disposed) {
            return;
        }
        
        if (disposing) {
            lock (_lock) {
                _logWriter?.Dispose();
                _indexWriter?.Dispose();
                _logStream?.Dispose();
                _indexStream?.Dispose();
            }
            
            // Clean up temp files
            try { File.Delete(_logFilePath); }
            catch {
                /* Ignore cleanup failures */
            }
            try { File.Delete(_indexFilePath); }
            catch {
                /* Ignore cleanup failures */
            }
        }
        
        _disposed = true;
        base.Dispose(disposing);
    }
}