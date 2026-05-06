using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace Lab1;

// Інкапсулює один прямокутник: стан, фонову задачу та логіку руху.
// Кожному екземпляру відповідає окрема Task, що рухає прямокутник доки
// не буде скасована натисканням миші всередині прямокутника.
public sealed class MovingRectangle
{
    private const double Size = 60;
    private const int FrameDelayMs = 16; // ~60 кадрів на секунду

    private readonly Border _view;
    private readonly AbsoluteLayout _container;
    private readonly Random _random;
    private readonly CancellationTokenSource _cts = new();
    private Task? _task;

    private double _x;
    private double _y;
    private double _dx;
    private double _dy;
    private double _boundsWidth;
    private double _boundsHeight;

    public int Id { get; }
    public event EventHandler? Closed;

    public MovingRectangle(int id, AbsoluteLayout container, Random random,
        double containerWidth, double containerHeight)
    {
        Id = id;
        _container = container;
        _random = random;
        _boundsWidth = containerWidth > Size ? containerWidth : 600;
        _boundsHeight = containerHeight > Size ? containerHeight : 400;

        var r = _random.Next(minValue: 80, maxValue: 256);
        var g = _random.Next(minValue: 80, maxValue: 256);
        var b = _random.Next(minValue: 80, maxValue: 256);
        var background = Color.FromRgb(red: r, green: g, blue: b);
        var stroke = Color.FromRgb(red: 256 - r, green: 256 - g, blue: 256 - b);

        _view = new Border
        {
            Background = background,
            Stroke = stroke,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            WidthRequest = Size,
            HeightRequest = Size,
            Padding = new Thickness(0),
            Content = new Label
            {
                Text = id.ToString(),
                TextColor = Colors.White,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        _view.GestureRecognizers.Add(item: tap);

        _x = _random.NextDouble() * (_boundsWidth - Size);
        _y = _random.NextDouble() * (_boundsHeight - Size);

        var speed = 80 + _random.NextDouble() * 120;
        var angle = _random.NextDouble() * Math.PI * 2;
        _dx = Math.Cos(angle) * speed;
        _dy = Math.Sin(angle) * speed;

        AbsoluteLayout.SetLayoutFlags(_view, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(_view, new Rect(x: _x, y: _y, width: Size, height: Size));
        _container.Add(_view);
    }

    public void UpdateBounds(double width, double height)
    {
        if (width > Size) _boundsWidth = width;
        if (height > Size) _boundsHeight = height;
    }

    // Створення та запуск фонової задачі (стиль прикладу 1.1 з методички).
    public void Start()
    {
        _task = new Task(action: () => Run(token: _cts.Token), cancellationToken: _cts.Token);
        _task.Start();
    }

    public void Cancel() => _cts.Cancel();

    private void Run(CancellationToken token)
    {
        var lastTime = DateTime.UtcNow;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var dt = (now - lastTime).TotalSeconds;
                lastTime = now;

                Step(dt: dt);

                // Оновлення UI лише з основного потоку — патерн з розділу 2.1.5.
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (token.IsCancellationRequested) return;
                    AbsoluteLayout.SetLayoutBounds(_view,
                        new Rect(x: _x, y: _y, width: Size, height: Size));
                });

                Task.Delay(millisecondsDelay: FrameDelayMs, cancellationToken: token)
                    .Wait(cancellationToken: token);
            }
        }
        catch (OperationCanceledException)
        {
            // Очікувано: користувач клікнув всередині прямокутника.
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _container.Remove(_view);
                Closed?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    private void Step(double dt)
    {
        _x += _dx * dt;
        _y += _dy * dt;

        if (_x < 0) { _x = 0; _dx = -_dx; }
        else if (_x + Size > _boundsWidth) { _x = _boundsWidth - Size; _dx = -_dx; }

        if (_y < 0) { _y = 0; _dy = -_dy; }
        else if (_y + Size > _boundsHeight) { _y = _boundsHeight - Size; _dy = -_dy; }
    }

    private void OnTapped(object? sender, TappedEventArgs e) => _cts.Cancel();
}
