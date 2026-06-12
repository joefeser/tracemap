namespace BrokenSample;

public sealed class BrokenProfile
{
    public string PrimaryEmail { get; init; } = "";

    public void Send(MissingContract contract)
    {
        contract.Deliver(PrimaryEmail);
    }
}
