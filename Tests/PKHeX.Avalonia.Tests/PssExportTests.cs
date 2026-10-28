using System.Buffers.Binary;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.UseCases;
using PKHeX.Avalonia.Tests.Harness;
using PKHeX.Avalonia.Views;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using PKHeX.Presentation.Localization;

namespace PKHeX.Avalonia.Tests;

public sealed class PssExportTests
{
    internal static SAV6XY MakeMultiRecordSave()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../savefiles/gen6_x.main"));
        var save = Assert.IsType<SAV6XY>(FileUtil.GetSupportedFile(path));
        WriteContact(save, 0, 0, 1, "Ada", "First");
        WriteContact(save, 0, 1, 2, "Bert", "Second");
        WriteContact(save, 2, 0, 3, "Cora", "Third");
        return save;
    }

    private static void WriteContact(SAV6XY save, int group, int index, ulong id, string trainer, string message)
    {
        var entry = save.Data.Slice(save.PSS + group * 0x5000 + index * 0xC8, 0xC8);
        entry.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(entry, id);
        Encoding.Unicode.GetBytes(trainer).CopyTo(entry.Slice(0x08, 0x1A));
        Encoding.Unicode.GetBytes(message).CopyTo(entry.Slice(0x22, 0x22));
        entry[0x5A] = (byte)GameVersion.X;
        entry[0x57] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(entry[0x9C..], 25);
    }

    [Fact]
    public void UpstreamParserIncludesEveryStructuredContactWithoutMutation()
    {
        var save = MakeMultiRecordSave();
        var before = save.Data.ToArray();
        var groups = new ExportPss6UseCase().Execute(save);

        var trainerLines = PSS6.GetPSSParse(save)
            .Where(line => line.StartsWith("OT: ", StringComparison.Ordinal));

        Assert.Equal(groups.SelectMany(group => group.Contacts).Select(contact => $"OT: {contact.Trainer}"),
            trainerLines);
        Assert.Equal(before, save.Data.ToArray());
    }

    [Fact]
    public void ParserPreservesMultipleRecordsAndGroupOrderWithoutMutation()
    {
        var save = MakeMultiRecordSave();
        var before = save.Data.ToArray();
        var groups = new ExportPss6UseCase().Execute(save);

        Assert.Equal(new[] { PssGroupKind.Friends, PssGroupKind.Acquaintances, PssGroupKind.Passerby },
            groups.Select(g => g.Kind));
        Assert.Equal(new[] { "Ada", "Bert" }, groups[0].Contacts.Select(c => c.Trainer));
        Assert.Empty(groups[1].Contacts);
        Assert.Equal("Cora", Assert.Single(groups[2].Contacts).Trainer);
        Assert.Equal("Second", groups[0].Contacts[1].Message);
        Assert.Equal(before, save.Data.ToArray());
    }

    [Fact]
    public async Task PreviewOnlyCopiesOrSavesAfterExplicitActions()
    {
        var save = MakeMultiRecordSave();
        var before = save.Data.ToArray();
        var dialogs = new RecordingDialogService();
        string? copied = null;
        var clipboard = new Mock<IClipboardService>();
        clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>()))
            .Callback<string>(text => copied = text).Returns(Task.CompletedTask);
        var vm = new PssExportViewModel(save, clipboard.Object, dialogs);

        Assert.Null(copied);
        Assert.Contains("Ada", vm.PreviewText, StringComparison.Ordinal);
        Assert.True(vm.PreviewText.IndexOf("Ada", StringComparison.Ordinal)
            < vm.PreviewText.IndexOf("Bert", StringComparison.Ordinal));
        await vm.CopyCommand.ExecuteAsync(null);
        Assert.Equal(vm.PreviewText, copied);
        Assert.Single(dialogs.Infos);

        var path = Path.Combine(Path.GetTempPath(), $"pkhex-pss-{Guid.NewGuid():N}.txt");
        try
        {
            dialogs.SaveFileResult = path;
            await vm.SaveTextCommand.ExecuteAsync(null);
            Assert.Equal(vm.PreviewText, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            Assert.Equal(2, dialogs.Infos.Count);
            Assert.Equal(before, save.Data.ToArray());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void PreviewAndActionsArePresentAtCompactSize()
    {
        var vm = new PssExportViewModel(MakeMultiRecordSave(), Mock.Of<IClipboardService>(), new RecordingDialogService());
        var view = new PssExportView { DataContext = vm, Width = 580, Height = 450 };
        var window = new Window { Content = view, Width = 600, Height = 470 };
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.Contains("Ada", Assert.IsType<TextBox>(view.FindControl<TextBox>("PreviewBox")).Text,
                StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MenuActionFollowsGen6PssCapability()
    {
        using var app = new HeadlessAppFixture();
        app.LoadSaveInstance(new SAV6XY());
        var action = Assert.Single(app.ViewModel.ToolMenuGroups.SelectMany(group => group.Items),
            item => item.Title == LocalizedStrings.Instance["Menu_Gen6_PSSExport"]);
        Assert.True(action.IsAvailable);

        app.LoadSaveInstance(new SAV5BW());
        Assert.False(action.IsAvailable);
    }
}
