using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace ComputeryTabgCLI;

internal static class Program {
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    private static Process? _serverProcess;
    private static IApplication _app = null!;
    private static NamedPipeServerStream? _pipeServer;
    private static StreamWriter? _pipeWriter;
    
    private static Window _top = null!;

    private static ServerView _serverView = null!;
    private static VisitorLogView _visitorLogView = null!;
    
    private static readonly Color AccentColor = new (0x8B, 0xE0, 0xFF);
    private static Scheme DefaultScheme => new() { };
    private static readonly Attribute LineAttr = new Attribute(AccentColor, Color.Black);
    private static readonly Scheme LineScheme = new() {
        Normal = LineAttr,
        HotNormal = LineAttr,
        Focus = LineAttr,
        HotFocus = LineAttr,
        Active = LineAttr,
        HotActive = LineAttr,
        Highlight = LineAttr,
        Disabled = LineAttr,
        Editable = LineAttr,
        ReadOnly = LineAttr,
    };


    public static void Main(string[] args) {
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;

        _app = Application.Create().Init();
        _top = new() { BorderStyle = LineStyle.None, };
        _top.SetScheme(DefaultScheme);

        SetupServerView();
        SetupButtons();

        _ = RunServerAsync(CancellationTokenSource.Token);

        try {
            _app.Run(_top);
        }
        finally { 
            _top.Dispose();
            _app.Dispose();
            CleanupServer();
        }
    }

    private static void SetupServerView() {
        _serverView = new ServerView();
        _serverView.ApplyBorderScheme(LineScheme);
        _serverView.CommandEntered += SendCommandToServer;
        _top.Add(_serverView);
        
        _visitorLogView = new VisitorLogView();
        _visitorLogView.ApplyBorderScheme(LineScheme);
        _visitorLogView.Visible = false;
        _top.Add(_visitorLogView);
        
        _app.AddTimeout(TimeSpan.FromMilliseconds(10), () => {
            _serverView.FlushLogBuffer();
            return true;
        });
    }

    private static void SetupButtons() {
        ComputeryButton serverTerminal = new ComputeryButton() {
            X = 0,
            Y = 0,
            Text = "Terminal",
            Title = "",
            ShadowStyle = ShadowStyle.None,
            BorderStyle = LineStyle.Rounded,
            NoDecorations = true,
            SuperViewRendersLineCanvas = true,
            IsDefault = true,
        };
        serverTerminal.Border?.SetScheme(LineScheme);
        serverTerminal.Accepting += (_, e) => {
            _serverView.Visible = true;
            _visitorLogView.Visible = false;
            e.Handled = true;
        };
        _top.Add(serverTerminal);

        ComputeryButton visitorLogButton = new ComputeryButton() {
            X = Pos.Right(serverTerminal) - 1,
            Y = 0,
            Text = "Visitor Log",
            Title = "",
            ShadowStyle = ShadowStyle.None,
            BorderStyle = LineStyle.Rounded,
            NoDecorations = true,
            SuperViewRendersLineCanvas = true,
        };
        visitorLogButton.Border?.SetScheme(LineScheme);
        visitorLogButton.Accepting += (_, e) => {
            _serverView.Visible = false;
            _visitorLogView.Visible = true;
            e.Handled = true;
        };
        _top.Add(visitorLogButton);
    }
    
    private static void OnProcessExit(object? sender, EventArgs e) { CleanupServer(); }
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) {
        e.Cancel = true;
        CleanupServer();
        Environment.Exit(0);
    }

    private static void CleanupServer() {
        try { CancellationTokenSource.Cancel(); }
        catch { /* Ingorned */ }

        try {
            _pipeWriter?.Dispose();
            _pipeServer?.Dispose();
        }
        catch { /* Ignored */ }

        if (_serverProcess != null && !_serverProcess.HasExited) {
            try {
                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess.WaitForExit(5000);
                _serverProcess.Dispose();
            }
            catch (Exception ex) { Console.Error.WriteLine($"Error closing server: {ex.Message}"); }
            finally { _serverProcess = null; }
        }
        CancellationTokenSource.Dispose();
    }
    
    private static async Task RunServerAsync(CancellationToken cancellationToken) {
        string unityAppPath = @"C:\Users\Computery\Desktop\LandfallPlzFix\Server\TABG.exe";
        string pipeGuid = Guid.NewGuid().ToString();

        StartServerProcess(unityAppPath, pipeGuid);
        SetupProcessEventHandlers();
        
        _ = HandlePipeCommunicationAsync(pipeGuid, cancellationToken);

        _serverView.LogLine("Unity process started.");
        try { await _serverProcess!.WaitForExitAsync(cancellationToken); } catch { /* Ignored */ }
        _serverView.LogLine("Unity process exited.");
    }

    private static void StartServerProcess(string unityAppPath, string pipeGuid) {
        _serverProcess = new Process();
        _serverProcess.StartInfo = new ProcessStartInfo {
            FileName = unityAppPath,
            Arguments = $"-pipeName {pipeGuid}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _serverProcess.EnableRaisingEvents = true;
        _serverProcess.Start();
    }

    private static void SetupProcessEventHandlers() {
        if (_serverProcess == null) return;

        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        _serverProcess.OutputDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data)) { _serverView.LogLine(e.Data); }
        };

        _serverProcess.ErrorDataReceived += (sender, e) => {
            if (!string.IsNullOrEmpty(e.Data)) { _serverView.LogLine(e.Data); }
        };
    }

    private static async Task HandlePipeCommunicationAsync(string pipeGuid, CancellationToken cancellationToken) {
        try {
            _pipeServer = new NamedPipeServerStream(pipeGuid, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            _serverView.LogLine("Waiting for Unity to connect...");

            await _pipeServer.WaitForConnectionAsync(cancellationToken);
            _pipeWriter = new StreamWriter(_pipeServer) { AutoFlush = true };
            _serverView.LogLine("Unity connected to pipe.");

            await ReadUnityMessagesAsync(cancellationToken);
        }
        catch (Exception ex) { _serverView.LogLine($"Pipe error: {ex.Message}"); }
    }

    private static async Task ReadUnityMessagesAsync(CancellationToken cancellationToken) {
        using StreamReader reader = new StreamReader(_pipeServer!, leaveOpen: true);
        while (!cancellationToken.IsCancellationRequested && _pipeServer?.IsConnected == true) {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line != null) {
                try {
                    using JsonDocument doc = JsonDocument.Parse(line);
                    JsonElement root = doc.RootElement;
                    
                    if (root.TryGetProperty("type", out JsonElement typeElement)) {
                        string? messageType = typeElement.GetString();
                        _serverView.LogLine($"Received message of type: {messageType}");
                    }
                }
                catch (Exception e) {
                    _serverView.LogLine($"Failed to parse message from Unity: {line}");
                    _serverView.LogLine($"Error: {e}");
                }
            }
        }
    }

    private static void SendCommandToServer(string command) {
        if (!string.IsNullOrWhiteSpace(command) && _pipeWriter != null) {
            try { _pipeWriter.WriteLine(command); }
            catch (Exception ex) { _serverView.LogLine($"Failed to send command: {ex.Message}"); }
        }
    }
}

