using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace ComputeryTabgCLI;

public sealed class ServerView : View {
    private readonly LogView _logView;
    private readonly TextField _commandInput;
    
    public event Action<string>? CommandEntered;
    
    public ServerView() {
        X = 0;
        Y = 2;
        Width = Dim.Fill();
        Height = Dim.Fill();
        SuperViewRendersLineCanvas = true;
        CanFocus = true;
        
        _logView = new LogView {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            WordWrap = true,
            AutoScroll = true,
            MaxLines = 10000,
            SuperViewRendersLineCanvas = true
        };
        Add(_logView);
        
        _commandInput = new TextField {
            X = 0,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Height = Dim.Absolute(3),
            BorderStyle = LineStyle.Rounded,
            SuperViewRendersLineCanvas = true
        };
        
        _commandInput.KeyDown += (_, e) => {
            if (e != Key.Enter) { return; }
            string text = _commandInput.Text;
            if (string.IsNullOrWhiteSpace(text)) { return; }
            CommandEntered?.Invoke(text);
            _commandInput.Text = string.Empty;
        };
        
        Add(_commandInput);
    }
    
    public void LogLine(string message) {
        _logView.LogLine(message);
    }
    
    public void FlushLogBuffer() {
        _logView.FlushLogBuffer();
    }
    
    public void ApplyBorderScheme(Scheme scheme) {
        _commandInput.Border?.SetScheme(scheme);
        _logView.Border?.SetScheme(scheme);
    }
}