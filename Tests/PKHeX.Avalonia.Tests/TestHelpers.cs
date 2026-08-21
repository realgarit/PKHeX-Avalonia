
using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Presentation.ViewModels;
using PKHeX.Core;

namespace PKHeX.Avalonia.Tests;

public static class TestHelpers
{
    public static (PokemonEditorViewModel VM, Mock<ISpriteRenderer> SpriteRenderer, Mock<IDialogService> DialogService) CreateTestViewModel(PKM pkm, SaveFile sav, bool haXMode = false)
    {
        var spriteRendererMock = new Mock<ISpriteRenderer>();
        var dialogServiceMock = new Mock<IDialogService>();
        var windowServiceMock = new Mock<IWindowService>();

        var vm = new PokemonEditorViewModel(pkm, sav, spriteRendererMock.Object, dialogServiceMock.Object, windowServiceMock.Object, haXMode);
        
        return (vm, spriteRendererMock, dialogServiceMock);
    }
}
