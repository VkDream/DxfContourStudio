#nullable enable

namespace DxfContourStudio.Application.Documents;

/// <summary>
/// The user's decision on the "unsaved changes" guard.
/// </summary>
public enum UnsavedPromptResult
{
    /// <summary>The user wants to save the current document first.</summary>
    Save,

    /// <summary>The user wants to discard the changes.</summary>
    Discard,

    /// <summary>The user cancelled the whole operation.</summary>
    Cancel,
}

/// <summary>
/// Abstraction over the "you have unsaved changes" question so the decision
/// flow is testable without a real message box: the Application layer asks
/// this interface and the WPF layer answers with the real dialog. The dirty
/// check itself lives in Application (see <see cref="UnsavedChangesGuard"/>).
/// </summary>
public interface IUnsavedChangesPrompt
{
    /// <summary>
    /// Asks the user what to do with unsaved changes. <paramref name="context"/>
    /// is a short description of the operation about to discard them.
    /// </summary>
    UnsavedPromptResult Ask(string context);
}

/// <summary>
/// Testable guard around operations that would discard a dirty document.
/// Callers invoke <see cref="ConfirmBeforeDiscard"/>; the prompt implementation
/// is injected so unit tests can simulate Save/Discard/Cancel.
/// </summary>
public sealed class UnsavedChangesGuard
{
    private readonly IUnsavedChangesPrompt _prompt;

    public UnsavedChangesGuard(IUnsavedChangesPrompt prompt)
    {
        _prompt = prompt;
    }

    /// <summary>
    /// Returns true when the operation may proceed. A clean document always
    /// proceeds; a dirty one asks the user (Save → returns true and reports
    /// the save intent; Discard → true; Cancel → false).
    /// </summary>
    public (bool Proceed, bool ShouldSave) ConfirmBeforeDiscard(
        CadDocument document, string context)
    {
        if (!document.IsDirty)
        {
            return (true, false);
        }

        return _prompt.Ask(context) switch
        {
            UnsavedPromptResult.Save => (true, true),
            UnsavedPromptResult.Discard => (true, false),
            _ => (false, false),
        };
    }
}
