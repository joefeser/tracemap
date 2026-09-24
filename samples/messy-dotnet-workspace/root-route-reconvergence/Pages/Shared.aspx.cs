namespace Recon.Pages;

public sealed class SharedPage
{
    protected void Run_Click(object sender, System.EventArgs e)
    {
        Recon.Services.RouteOne.Run();
        Recon.Services.RouteTwo.Run();
    }
}
