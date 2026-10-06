namespace TraceMap.CompiledFixtures.Equivalence;

public class EventEmitter
{
    public event Action? Tick;
    public void Raise() => Tick?.Invoke();
}

public class EventSubscriber
{
    private EventEmitter? source;
    public EventEmitter? Source
    {
        get => source;
        set
        {
            if (source is not null) source.Tick -= OnTick;
            source = value;
            if (source is not null) source.Tick += OnTick;
        }
    }
    private void OnTick() { }
}
