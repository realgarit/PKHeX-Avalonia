using Avalonia.Headless.XUnit;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

public class EncounterToolTests
{
    [AvaloniaFact]
    public async Task EncounterTool_ReusesSession_AndRejectsOldSessionSelections()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        app.ViewModel.OpenEncounterDatabaseCommand.Execute(null);
        var database = Assert.IsType<EncounterDatabaseViewModel>(app.Windows.ShownTools.Single().ViewModel);
        app.ViewModel.OpenEncounterDatabaseCommand.Execute(null);
        Assert.Same(database, Assert.Single(app.Windows.FocusedTools));
        Assert.Empty(app.Windows.ShownDialogs);
        database.SelectedSpecies = 25;
        await database.SearchCommand.ExecuteAsync(null);
        var result = Assert.Single(database.Results.Take(1));
        await database.SelectEncounterCommand.ExecuteAsync(result);
        Assert.Equal(result.Encounter.Species, app.ViewModel.CurrentPokemonEditor!.TargetPKM.Species);

        app.LoadSaveInstance(new SAV6XY());
        Assert.Equal(0, app.Windows.ActiveToolCount);
        var before = app.ViewModel.CurrentPokemonEditor!.TargetPKM.Species;
        await database.SelectEncounterCommand.ExecuteAsync(result);
        Assert.Equal(before, app.ViewModel.CurrentPokemonEditor.TargetPKM.Species);
        app.ViewModel.OpenEncounterDatabaseCommand.Execute(null);
        Assert.NotSame(database, app.Windows.ShownTools.Last().ViewModel);
        app.ViewModel.CloseFileCommand.Execute(null);
        Assert.Equal(0, app.Windows.ActiveToolCount);
    }
}
