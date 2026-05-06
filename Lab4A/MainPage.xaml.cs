using System.Diagnostics;
using Lab4Shared;

namespace Lab4A;

public partial class MainPage : ContentPage
{
    private const double Radius = 20;
    private const double Speed = 3;
    private const int FrameDelayMs = 16;
    private const double DefaultY = 200;

    private readonly CircleChannel _channel;
    private readonly IDispatcherTimer _timer;
    private double _x;
    private double _y = DefaultY;
    private double _canvasWidth = 800;
    private bool _handedOff;
    private Process? _childProcess;

    public MainPage()
    {
        InitializeComponent();

        // Створюємо спільні ресурси (MMF + Mutex + Event) — Lab4B відкриватиме як OpenExisting.
        _channel = CircleChannel.Create();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(value: FrameDelayMs);
        _timer.Tick += Timer_Tick;

        // Запускаємо Lab4B одразу — на рівні з прикладом 4.3 з методички (батьківський A запускає B).
        TryLaunchPartner();
    }

    private void TryLaunchPartner()
    {
        var aPath = Environment.ProcessPath;
        if (aPath is null)
        {
            DeferStatus(text: "Не вдалося визначити шлях A — запустіть Lab4B вручну.");
            return;
        }

        // Шлях до B обчислюється з шляху A заміною назви проекту.
        var bPath = aPath.Replace(oldValue: "Lab4A", newValue: "Lab4B");
        if (!File.Exists(path: bPath))
        {
            DeferStatus(text: $"Lab4B.exe не знайдено: '{bPath}'. Запустіть його вручну (dotnet run --project Lab4B).");
            return;
        }

        try
        {
            _childProcess = Process.Start(fileName: bPath);
            DeferStatus(text: $"Lab4B запущено (PID {_childProcess?.Id}). Натисни 'Старт'.");
        }
        catch (Exception ex)
        {
            DeferStatus(text: $"Не вдалося запустити Lab4B: {ex.Message}");
        }
    }

    // У конструкторі StatusLabel ще може бути недоступний — відкладаємо оновлення.
    private void DeferStatus(string text) =>
        Dispatcher.Dispatch(() => StatusLabel.Text = text);

    private void Canvas_SizeChanged(object? sender, EventArgs e)
    {
        _canvasWidth = Canvas.Width;
        if (Canvas.Height > 0 && _y + Radius * 2 > Canvas.Height)
            _y = Math.Max(val1: 0, val2: Canvas.Height - Radius * 2);

        if (!_timer.IsRunning)
            ApplyCirclePosition();
    }

    private void StartButton_Clicked(object? sender, EventArgs e)
    {
        if (_handedOff)
        {
            ResetCircle();
            _handedOff = false;
        }
        if (!_timer.IsRunning)
        {
            StatusLabel.Text = "Анімація: коло рухається до правого краю...";
            _timer.Start();
        }
    }

    private void ResetButton_Clicked(object? sender, EventArgs e)
    {
        _timer.Stop();
        ResetCircle();
        _handedOff = false;
        StatusLabel.Text = "Скинуто. Натисни 'Старт'.";
    }

    private void ResetCircle()
    {
        _x = 0;
        ApplyCirclePosition();
    }

    private void ApplyCirclePosition()
    {
        AbsoluteLayout.SetLayoutBounds(bindable: Ball,
            bounds: new Rect(x: _x, y: _y, width: Radius * 2, height: Radius * 2));
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _x += Speed;

        if (_x + Radius * 2 >= _canvasWidth)
        {
            _x = _canvasWidth - Radius * 2;
            ApplyCirclePosition();
            _timer.Stop();
            HandOffToB();
            return;
        }

        ApplyCirclePosition();
    }

    private void HandOffToB()
    {
        _handedOff = true;
        try
        {
            _channel.SendHandoff(y: _y, radius: Radius);
            StatusLabel.Text = $"Передано Lab4B: y={_y:F0}, radius={Radius:F0}.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Помилка передачі через MMF: {ex.Message}";
        }
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args: args);
        if (args.NewHandler is null)
        {
            _timer.Stop();
            _channel.Dispose();
            try { _childProcess?.Dispose(); } catch { }
        }
    }
}
