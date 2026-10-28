using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Avalonia.Controls;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.Localization;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Render.Tests;

public sealed class SearchableAbilityRenderTests
{
    [AvaloniaTheory]
    [InlineData("en", false)] [InlineData("en", true)]
    [InlineData("ja", false)] [InlineData("ja", true)]
    public async Task DuplicateAbilitySlotsUseTheActualSearchableEditorInBothThemes(string language, bool dark)
    {
        var oldLanguage = LocalizedStrings.Instance.CurrentLanguage;
        var app = global::Avalonia.Application.Current!;
        var oldTheme = app.RequestedThemeVariant;
        Window? window = null;
        try
        {
            LocalizedStrings.Instance.SetLanguage(language);
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var save = new SAV9SV();
            GameInfo.FilteredSources = new FilteredGameDataSource(save, GameInfo.Sources);
            var pk = new PK9 { Species = (ushort)Species.Mew, CurrentLevel = 50 };
            pk.RefreshAbility(0);
            var vm = new PokemonEditorViewModel(pk, save, Mock.Of<ISpriteRenderer>(), Mock.Of<IDialogService>(), Mock.Of<IWindowService>());
            var view = new PokemonEditor { DataContext = vm };
            window = new Window { Content = view, Width = 900, Height = 750 };
            window.Show();
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            var picker = view.GetVisualDescendants().OfType<FilterableComboBox>()
                .Single(control => AutomationProperties.GetName(control) == LocalizedStrings.Instance["PokemonEditor_Ability"]);
            var text = picker.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.True(picker.Bounds.Width > 100);
            Assert.True(picker.Bounds.Height >= 28);
            for (var slot = 0; slot < 3; slot++)
            {
                vm.SelectedAbilityIndex = slot;
                await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
                Assert.Equal(vm.AbilityChoices[slot].Text, text.Text);
                Assert.Equal(1 << slot, vm.PreparePKM().AbilityNumber);
                using var frame = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1));
                frame.Render(window);
                if (Environment.GetEnvironmentVariable("PKHEX_HEADLESS_CAPTURE_DIR") is { } directory)
                {
                    Directory.CreateDirectory(directory);
                    frame.Save(Path.Combine(directory, $"ability-{language}-{dark}-{slot}.png"), PngBitmapEncoderOptions.Default);
                }
            }
            picker.MinimumPopulateDelay = TimeSpan.Zero;
            // AutoCompleteBox forwards focus to its TextBox; Avalonia 12 Focus() then returns false.
            picker.Focus();
            Assert.True(picker.IsKeyboardFocusWithin);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            window.KeyTextInput(vm.AbilityList[0].Text.Split(' ')[0]);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.True(picker.IsDropDownOpen);
            Assert.Equal(2, vm.SelectedAbilityIndex);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            await PKHeX.Testing.HeadlessRenderSettling.Settle(window);
            Assert.Equal(vm.AbilityChoices[2].Text, picker.Text);
        }
        finally
        {
            window?.Close();
            app.RequestedThemeVariant = oldTheme;
            LocalizedStrings.Instance.SetLanguage(oldLanguage);
        }
    }
}
