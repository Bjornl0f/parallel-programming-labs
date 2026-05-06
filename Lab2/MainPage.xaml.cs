using System.Diagnostics;

namespace Lab2;

public partial class MainPage : ContentPage
{
    private int _activeCount;
    private readonly List<string> _logLines = [];

    public MainPage()
    {
        InitializeComponent();
    }

    private async void BrowseButton_Clicked(object? sender, EventArgs e)
    {
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = new[] { ".xlsx", ".xls", ".xlsm", ".xlsb", ".csv" },
            });
            var pickResult = await FilePicker.PickAsync(options: new PickOptions
            {
                PickerTitle = "Оберіть файл Excel",
                FileTypes = fileTypes,
            });
            if (pickResult is not null)
                FilePathEntry.Text = pickResult.FullPath;
        }
        catch (Exception ex)
        {
            AppendLog($"Помилка вибору файлу: {ex.Message}");
        }
    }

    // Запуск Excel (приклад 2.1.3 з методички) + неблокувальне очікування завершення (2.1.4).
    private void OpenButton_Clicked(object? sender, EventArgs e)
    {
        var path = FilePathEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value: path))
        {
            AppendLog(message: "Не вказано шлях до файлу.");
            return;
        }
        if (!File.Exists(path: path))
        {
            AppendLog(message: $"Файл не знайдено: {path}");
            return;
        }

        var fileName = Path.GetFileName(path: path);

        var startInfo = new ProcessStartInfo
        {
            FileName = "excel.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true,
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        process.Exited += (s, ev) =>
        {
            // Подія Exited приходить з пула потоків — UI оновлюємо лише з основного.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _activeCount--;
                AppendLog(message: $"Файл {fileName} відредагований в Excel");
                UpdateStatus();
                process.Dispose();
            });
        };

        try
        {
            var started = process.Start();
            if (!started)
            {
                AppendLog(message: $"Excel перенаправив запит до існуючого вікна — відстеження закриття для {fileName} недоступне.");
                process.Dispose();
                return;
            }

            _activeCount++;
            AppendLog(message: $"Запущено Excel для {fileName} (PID {process.Id}, пріоритет {process.PriorityClass}).");
            UpdateStatus();
        }
        catch (Exception ex)
        {
            AppendLog(message: $"Не вдалося запустити Excel: {ex.Message}");
            process.Dispose();
        }
    }

    private void ClearLogButton_Clicked(object? sender, EventArgs e)
    {
        _logLines.Clear();
        LogLabel.Text = string.Empty;
    }

    private void UpdateStatus() =>
        StatusLabel.Text = $"Активних процесів: {_activeCount}";

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logLines.Insert(index: 0, item: line);
        if (_logLines.Count > 200) _logLines.RemoveRange(index: 200, count: _logLines.Count - 200);
        LogLabel.Text = string.Join(separator: Environment.NewLine, values: _logLines);
    }
}
