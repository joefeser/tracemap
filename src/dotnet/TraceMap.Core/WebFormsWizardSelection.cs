using System.Text;

namespace TraceMap.Core;

public sealed record WebFormsWizardSelectionResult(string ProjectId, string Step, string? EditFile, bool Paused);

/// <summary>Shared subset pause/resume transition. It never runs a build or a scan.</summary>
public static class WebFormsWizardSelection
{
    public static WebFormsWizardSelectionResult Advance(WebFormsWizardStore store, string projectId)
    {
        var project = store.ReadProject(projectId);
        // Reclassify on every continuation; a saved cursor does not bypass target validation.
        var target = WebFormsWizardTarget.Inspect(project.InputPath, project.WebRoot);
        if (target.ProjectMode != project.ProjectMode) throw new InvalidOperationException("WEBFORMS_WIZARD_TARGET_CHANGED");
        if (project.Step != "forms")
        {
            if (project.FormsMode == "selected") _ = WebFormsWizardForms.Parse(project.WebRoot, string.Join('\n', project.Forms));
            return new(project.Id, project.Step, null, false);
        }
        string[] forms;
        if (project.FormsMode == "all") forms = WebFormsWizardForms.Discover(project.WebRoot).ToArray();
        else
        {
            var path = Path.Combine(Path.GetDirectoryName(store.ProjectPath(project.Id))!, "forms.txt");
            if (new FileInfo(path).LinkTarget is not null) throw new InvalidOperationException("WEBFORMS_WIZARD_SELECTION_LINKED");
            string? text = null;
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > WebFormsWizardForms.MaxSelectionBytes) throw new InvalidOperationException("WEBFORMS_WIZARD_SELECTION_LIMIT");
                var bytes = new byte[(int)stream.Length];
                stream.ReadExactly(bytes);
                if (stream.ReadByte() != -1) throw new InvalidOperationException("WEBFORMS_WIZARD_SELECTION_CHANGED");
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            if (IsBlank(text))
            {
                var template = WebFormsWizardForms.Template(project.WebRoot);
                store.VerifyUnchanged();
                var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var pending = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { pending.Write(Encoding.UTF8.GetBytes(template)); pending.Flush(true); }
                    if (!File.Exists(path)) File.Move(staging, path, overwrite: false);
                    else
                    {
                        // ReplaceFile on Windows needs read/delete sharing on the destination.
                        // Deny in-place writers while checking that the human selection is blank;
                        // use replacement rather than MoveFileEx over an open destination.
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                        if (stream.Length > WebFormsWizardForms.MaxSelectionBytes) throw new InvalidOperationException("WEBFORMS_WIZARD_SELECTION_CHANGED");
                        var prior = new byte[(int)stream.Length];
                        stream.ReadExactly(prior);
                        if (!IsBlank(new UTF8Encoding(false, true).GetString(prior)))
                            throw new InvalidOperationException("WEBFORMS_WIZARD_SELECTION_CHANGED");
                        File.Replace(staging, path, destinationBackupFileName: null);
                    }
                }
                finally { if (File.Exists(staging)) File.Delete(staging); }
                return new(project.Id, "forms", path, true);
            }
            forms = WebFormsWizardForms.Parse(project.WebRoot, text).ToArray();
        }
        store.SaveProject(project with { Forms = forms, Step = "build" });
        return new(project.Id, "build", null, false);
    }

    private static bool IsBlank([System.Diagnostics.CodeAnalysis.NotNullWhen(false)] string? text) =>
        text is null || string.IsNullOrWhiteSpace(text.TrimStart('\uFEFF'));
}
