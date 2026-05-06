using Lab4Shared;

namespace Lab4B;

public partial class MainPage : ContentPage
{
    private const double Speed = 3;
    private const int FrameDelayMs = 16;

    private readonly CancellationTokenSource _cts = new();
    private CircleChannel? _channel;
    private IDispatcherTimer? _timer;

    private double _x;
    private double _y;
    private double _radius;
    private double _canvasWidth = 800;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        // Очікуємо появи каналу: A може бути ще не запущена.
        var thread = new Thread(start: ConnectAndWaitLoop)
        {
            IsBackground = true,
            Name = "Lab4B-Listener",
        };
        thread.Start();
    }

    private void ConnectAndWaitLoop()
    {
        var token = _cts.Token;

        // Чекаємо поки Lab4A створить ресурси (повтори кожні 500 мс).
        while (!token.IsCancellationRequested)
        {
            try
            {
                _channel = CircleChannel.OpenExisting();
                break;
            }
            catch (FileNotFoundException)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusLabel.Text = "Очікування Lab4A (канал ще не створено)...");
                if (token.WaitHandle.WaitOne(millisecondsTimeout: 500)) return;
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusLabel.Text = $"Помилка підключення: {ex.Message}");
                return;
            }
        }

        if (_channel is null) return;

        MainThread.BeginInvokeOnMainThread(() =>
            StatusLabel.Text = "Підключено. Очікування передачі від Lab4A...");

        // Цикл прийому: чекаємо сигнал, читаємо payload, запускаємо анімацію в UI.
        while (!token.IsCancellationRequested)
        {
            CircleHandoff? handoff;
            try
            {
                handoff = _channel.WaitForHandoff(token: token);
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusLabel.Text = $"Помилка читання: {ex.Message}");
                return;
            }
            if (handoff is null) return;
            if (!handoff.Value.Ready) continue;

            var data = handoff.Value;
            MainThread.BeginInvokeOnMainThread(() => StartAnimation(y: data.Y, radius: data.Radius));
        }
    }

    private void Canvas_SizeChanged(object? sender, EventArgs e) =>
        _canvasWidth = Canvas.Width;

    private void StartAnimation(double y, double radius)
    {
        _y = Math.Max(val1: 0, val2: Math.Min(val1: y, val2: Math.Max(val1: 0, val2: Canvas.Height - radius * 2)));
        _radius = radius;
        _x = 0;

        Ball.WidthRequest = radius * 2;
        Ball.HeightRequest = radius * 2;
        Ball.IsVisible = true;
        ApplyCirclePosition();

        StatusLabel.Text = $"Отримано від A: y={_y:F0}, radius={_radius:F0}. Анімація...";

        _timer ??= CreateTimer();
        if (!_timer.IsRunning) _timer.Start();
    }

    private IDispatcherTimer CreateTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(value: FrameDelayMs);
        timer.Tick += Timer_Tick;
        return timer;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _x += Speed;
        if (_x + _radius * 2 >= _canvasWidth)
        {
            _x = _canvasWidth - _radius * 2;
            ApplyCirclePosition();
            _timer?.Stop();
            StatusLabel.Text = $"Анімація завершена (y={_y:F0}, radius={_radius:F0}). Очікую наступний сигнал від A...";
            return;
        }
        ApplyCirclePosition();
    }

    private void ApplyCirclePosition() =>
        AbsoluteLayout.SetLayoutBounds(bindable: Ball,
            bounds: new Rect(x: _x, y: _y, width: _radius * 2, height: _radius * 2));

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args: args);
        if (args.NewHandler is null)
        {
            _cts.Cancel();
            _timer?.Stop();
            _channel?.Dispose();
        }
    }
}
