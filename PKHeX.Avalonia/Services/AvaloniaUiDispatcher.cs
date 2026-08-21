using Avalonia.Threading;
using PKHeX.Application.Abstractions;

namespace PKHeX.Avalonia.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
