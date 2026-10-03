namespace Beta.Api;

public sealed class StatusHandler
{
    public void ProcessRequest(object context)
    {
        Beta.Services.Gateway.Process();
    }
}
