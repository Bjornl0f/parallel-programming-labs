using System.IO.MemoryMappedFiles;

namespace Lab4Shared;

// Канал міжпроцесного зв'язку через memory-mapped file (тема Lab 4 — приклад 4.3 з методички).
// Імена ресурсів спільні між Lab4A та Lab4B; іменовані MMF/Mutex/Event автоматично доступні
// будь-якому процесу в межах сесії Windows.
public sealed class CircleChannel : IDisposable
{
    public const string MmfName = "Lab4_CircleHandoff_Mmf";
    public const string MutexName = "Lab4_CircleHandoff_Mutex";
    public const string EventName = "Lab4_CircleHandoff_Event";
    public const int Capacity = 4096;

    private readonly MemoryMappedFile _mmf;
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;

    private CircleChannel(MemoryMappedFile mmf, Mutex mutex, EventWaitHandle signal)
    {
        _mmf = mmf;
        _mutex = mutex;
        _signal = signal;
    }

    // Викликає батьківський процес (Lab4A): створює нові спільні ресурси.
    public static CircleChannel Create()
    {
        var mmf = MemoryMappedFile.CreateNew(mapName: MmfName, capacity: Capacity);
        var mutex = new Mutex(initiallyOwned: false, name: MutexName, createdNew: out _);
        var signal = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset,
            name: EventName, createdNew: out _);
        return new CircleChannel(mmf, mutex, signal);
    }

    // Викликає дочірній процес (Lab4B): відкриває вже існуючі ресурси.
    // Кидає FileNotFoundException якщо A ще не запускався.
    public static CircleChannel OpenExisting()
    {
        var mmf = MemoryMappedFile.OpenExisting(mapName: MmfName);
        var mutex = Mutex.OpenExisting(name: MutexName);
        var signal = EventWaitHandle.OpenExisting(name: EventName);
        return new CircleChannel(mmf, mutex, signal);
    }

    // Lab4A: під мютексом записує (true, y, radius) у MMF, потім сигналізує B.
    public void SendHandoff(double y, double radius)
    {
        _mutex.WaitOne();
        try
        {
            using var stream = _mmf.CreateViewStream();
            using var writer = new BinaryWriter(output: stream);
            writer.Write(value: true);   // ready flag
            writer.Write(value: y);
            writer.Write(value: radius);
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
        _signal.Set();
    }

    // Lab4B: чекає сигнал, потім читає payload. Повертає null при скасуванні.
    public CircleHandoff? WaitForHandoff(CancellationToken token)
    {
        var handles = new WaitHandle[] { _signal, token.WaitHandle };
        int idx = WaitHandle.WaitAny(waitHandles: handles);
        if (idx == 1) return null;

        _mutex.WaitOne();
        try
        {
            using var stream = _mmf.CreateViewStream();
            using var reader = new BinaryReader(input: stream);
            bool ready = reader.ReadBoolean();
            double y = reader.ReadDouble();
            double radius = reader.ReadDouble();
            return new CircleHandoff(Ready: ready, Y: y, Radius: radius);
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }

    public void Dispose()
    {
        _signal.Dispose();
        _mutex.Dispose();
        _mmf.Dispose();
    }
}

public readonly record struct CircleHandoff(bool Ready, double Y, double Radius);
