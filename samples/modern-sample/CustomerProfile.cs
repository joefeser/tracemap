namespace ModernSample;

public sealed class CustomerProfile
{
    public string PrimaryEmail { get; init; } = "";
}

public sealed class ProfileReporter
{
    public int Measure(CustomerProfile profile)
    {
        return profile.PrimaryEmail.Trim().Length;
    }
}
