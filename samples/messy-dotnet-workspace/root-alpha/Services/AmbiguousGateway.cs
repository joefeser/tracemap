namespace Alpha.Services;

// Two overloads and two implementations deliberately share simple names. The
// interface call identifies the signature, but its runtime receiver is unknown.
public interface IAmbiguousGateway
{
    void Process(int value);
    void Process(string value);
}

public sealed class NumericGateway : IAmbiguousGateway
{
    public void Process(int value) => Core(value);
    public void Process(string value) => Core(value);

    private void Core(int value) =>
        _ = new Alpha.Data.TextCommand("SELECT Id FROM numeric_int_queue WHERE Id = @id").ExecuteReader();

    private void Core(string value) =>
        _ = new Alpha.Data.TextCommand("SELECT Id FROM numeric_text_queue WHERE Id = @id").ExecuteReader();
}

public sealed class TextGateway : IAmbiguousGateway
{
    public void Process(int value) => Core(value);
    public void Process(string value) => Core(value);

    private void Core(int value) =>
        _ = new Alpha.Data.TextCommand("SELECT Id FROM text_int_queue WHERE Id = @id").ExecuteReader();

    private void Core(string value) =>
        _ = new Alpha.Data.TextCommand("SELECT Id FROM text_text_queue WHERE Id = @id").ExecuteReader();
}
