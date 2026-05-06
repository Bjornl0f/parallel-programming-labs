namespace Lab3;

public partial class MainPage : ContentPage
{
    // Спільний стан, що пишеться UI-потоком і читається трьома робочими.
    // Доступ синхронізується через lock (приклад 3.1 з методички).
    private readonly Lock _colorLock = new();
    private byte _r;
    private byte _g;
    private byte _b;

    // Сигнали від UI до робочих потоків (приклад 3.3 з методички — AutoResetEvent).
    // Окремий event на кожен компонент, щоб робочі потоки прокидалися незалежно.
    private readonly AutoResetEvent _redReady = new(initialState: false);
    private readonly AutoResetEvent _greenReady = new(initialState: false);
    private readonly AutoResetEvent _blueReady = new(initialState: false);

    private readonly CancellationTokenSource _cts = new();
    private readonly Random _random = new();

    public MainPage()
    {
        InitializeComponent();

        // Три робочі потоки — кожен відповідає за свій квадрант (R, G або B).
        StartWorker(name: "R", signal: _redReady);
        StartWorker(name: "G", signal: _greenReady);
        StartWorker(name: "B", signal: _blueReady);
    }

    private void StartWorker(string name, AutoResetEvent signal)
    {
        var thread = new Thread(start: () => Worker(component: name, signal: signal))
        {
            IsBackground = true,
            Name = $"Quad-{name}-Worker",
        };
        thread.Start();
    }

    private void Quad1_Tapped(object? sender, TappedEventArgs e)
    {
        byte r, g, b;

        lock (_colorLock)
        {
            _r = (byte)_random.Next(minValue: 0, maxValue: 256);
            _g = (byte)_random.Next(minValue: 0, maxValue: 256);
            _b = (byte)_random.Next(minValue: 0, maxValue: 256);
            r = _r; g = _g; b = _b;
        }

        var color = Color.FromRgb(red: r, green: g, blue: b);
        Quad1View.Color = color;
        Quad1Label.Text = $"RGB({r}, {g}, {b})";
        Quad1Label.TextColor = (r + g + b > 384) ? Colors.Black : Colors.White;
        InfoLabel.Text = $"Згенеровано: RGB({r}, {g}, {b}) — потоки оновлюють квадранти...";

        // Будимо всі три робочі потоки одночасно.
        _redReady.Set();
        _greenReady.Set();
        _blueReady.Set();
    }

    private void Worker(string component, AutoResetEvent signal)
    {
        // Чекаємо або сигнал від UI про новий клік, або скасування.
        var handles = new WaitHandle[] { signal, _cts.Token.WaitHandle };

        while (!_cts.IsCancellationRequested)
        {
            int idx = WaitHandle.WaitAny(waitHandles: handles);
            if (idx == 1) return; // CancellationToken signaled

            // Імітація роботи — щоб видно було паралельність трьох потоків.
            Thread.Sleep(millisecondsTimeout: _random.Next(minValue: 50, maxValue: 350));

            // Читаємо тільки свій компонент під замком, щоб гарантовано побачити
            // запис UI-потоку (і не змішатися з можливим наступним кліком).
            byte value;
            lock (_colorLock)
            {
                value = component switch
                {
                    "R" => _r,
                    "G" => _g,
                    _ => _b,
                };
            }

            const byte zero = 0;
            var color = component switch
            {
                "R" => Color.FromRgb(red: value, green: zero, blue: zero),
                "G" => Color.FromRgb(red: zero, green: value, blue: zero),
                _ => Color.FromRgb(red: zero, green: zero, blue: value),
            };
            var labelText = component switch
            {
                "R" => $"({value}, 0, 0)",
                "G" => $"(0, {value}, 0)",
                _ => $"(0, 0, {value})",
            };

            // UI оновлюємо лише з основного потоку (патерн з методички 1, розділ 2.1.5).
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (component)
                {
                    case "R":
                        Quad2View.Color = color;
                        Quad2Label.Text = labelText;
                        break;
                    case "G":
                        Quad3View.Color = color;
                        Quad3Label.Text = labelText;
                        break;
                    case "B":
                        Quad4View.Color = color;
                        Quad4Label.Text = labelText;
                        break;
                }
            });
        }
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.NewHandler is null)
            _cts.Cancel();
    }
}
