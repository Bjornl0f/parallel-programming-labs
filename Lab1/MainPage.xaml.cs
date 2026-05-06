namespace Lab1;

public partial class MainPage : ContentPage
{
    private readonly List<MovingRectangle> _rectangles = [];
    private readonly Random _random = new();
    private int _nextId = 1;

    public MainPage()
    {
        InitializeComponent();
    }

    private void Container_SizeChanged(object? sender, EventArgs e)
    {
        var width = Container.Width;
        var height = Container.Height;
        foreach (var rect in _rectangles)
            rect.UpdateBounds(width: width, height: height);
    }

    private void ApplyButton_Clicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(s: CountEntry.Text, result: out int target) || target < 0)
        {
            CountEntry.Text = _rectangles.Count.ToString();
            return;
        }

        while (_rectangles.Count < target) AddRectangle();
        while (_rectangles.Count > target) RemoveLastRectangle();

        UpdateStatus();
    }

    private void AddButton_Clicked(object? sender, EventArgs e)
    {
        AddRectangle();
        UpdateStatus();
    }

    private void RemoveButton_Clicked(object? sender, EventArgs e)
    {
        RemoveLastRectangle();
        UpdateStatus();
    }

    private void AddRectangle()
    {
        var rect = new MovingRectangle(
            id: _nextId++,
            container: Container,
            random: _random,
            containerWidth: Container.Width,
            containerHeight: Container.Height);
        rect.Closed += OnRectangleClosed;
        _rectangles.Add(item: rect);
        rect.Start();
    }

    private void RemoveLastRectangle()
    {
        if (_rectangles.Count == 0) return;
        var last = _rectangles[^1];
        _rectangles.RemoveAt(index: _rectangles.Count - 1);
        last.Cancel();
    }

    private void OnRectangleClosed(object? sender, EventArgs e)
    {
        if (sender is not MovingRectangle rect) return;
        _rectangles.Remove(item: rect);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusLabel.Text = $"Активних задач: {_rectangles.Count}";
    }
}
