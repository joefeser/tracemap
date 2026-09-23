namespace Alpha.Pages;

public class EnginesPage
{
    protected void DeepButton_Click(object sender, System.EventArgs e)
    {
        Alpha.Services.DeepChain.Run();
    }

    protected void LoopButton_Click(object sender, System.EventArgs e)
    {
        Alpha.Services.Loop.Enter();
    }

    protected void SelfButton_Click(object sender, System.EventArgs e)
    {
        Alpha.Services.Loop.Self();
    }

    protected void EnginesButton_Click(object sender, System.EventArgs e)
    {
        new Alpha.Services.Engine01().Process();
        new Alpha.Services.Engine02().Process();
        new Alpha.Services.Engine03().Process();
        new Alpha.Services.Engine04().Process();
        new Alpha.Services.Engine05().Process();
        new Alpha.Services.Engine06().Process();
        new Alpha.Services.Engine07().Process();
        new Alpha.Services.Engine08().Process();
        new Alpha.Services.Engine09().Process();
        new Alpha.Services.Engine10().Process();
    }

    protected void AmbiguityButton_Click(object sender, System.EventArgs e)
    {
        Alpha.Services.IAmbiguousGateway gateway = sender is null
            ? new Alpha.Services.NumericGateway()
            : new Alpha.Services.TextGateway();
        gateway.Process(1);
        gateway.Process("one");
    }
}
