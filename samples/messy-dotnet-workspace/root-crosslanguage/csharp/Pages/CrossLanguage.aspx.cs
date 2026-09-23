namespace CrossLanguage.CSharp;

public class CrossLanguagePage
{
    protected void CrossLanguageButton_Click(object sender, System.EventArgs e)
    {
        _ = CrossLanguage.VisualBasic.VbBridge.Run(17);
    }
}
