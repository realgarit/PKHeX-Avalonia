namespace PKHeX.Application.Abstractions;

/// <summary>
/// Marshals presentation updates to the host UI thread without coupling application or presentation
/// code to a specific UI framework.
/// </summary>
public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action);
}
