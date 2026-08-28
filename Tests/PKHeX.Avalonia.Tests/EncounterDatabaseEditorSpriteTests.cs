using Moq;
using PKHeX.Avalonia.Services;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;
using SkiaSharp;
using Xunit.Abstractions;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// GitHub issue #234 (Discord support report from BlvckFr0st, 2026-08-25): after loading an Aipom
/// result from the Encounter Database and changing the Species to Ambipom, the editor title and the
/// Species field read "Ambipom" while the preview sprite rendered Ariados.
///
/// Root cause: <see cref="EncounterDatabaseViewModel"/> hands the editor the encounter's <em>native</em>
/// format (<c>IEncounterConvertible.ConvertToPKM</c> returns a <see cref="PK2"/> for a Gold/Silver/Crystal
/// wild slot even when the open save is Gen 7+), while the editor's Species dropdown comes from the
/// save-scoped <see cref="GameInfo.FilteredSources"/>. <c>PK2.Species</c> is a single byte, so writing
/// Ambipom (424) stored <c>424 &amp; 0xFF</c> = 168 — Ariados — and the sprite, which renders from the
/// stored entity, was correct about a wrong entity.
/// </summary>
public class EncounterDatabaseEditorSpriteTests(ITestOutputHelper output)
{
    private const ushort Aipom = 190;
    private const ushort Ambipom = 424;
    private const ushort Ariados = 168;

    /// <summary>
    /// Pins the arithmetic behind the report: Ariados is not an arbitrary wrong species, it is
    /// exactly what Ambipom truncates to in a Generation 2 entity.
    /// </summary>
    [Fact]
    public void Gen2Entity_CannotStoreAmbipom_AndTruncatesToAriados()
    {
        var pk2 = new PK2 { Species = Ambipom };
        Assert.Equal(Ariados, pk2.Species);
        Assert.Equal(Ambipom & 0xFF, pk2.Species);
    }

    /// <summary>
    /// The editor's dropdowns are scoped to the save file, so the entity it edits must be in the
    /// save's format. A Gen 2 entity handed in from an outside source has to be adapted first.
    /// </summary>
    [Fact]
    public void LoadPKM_Gen2Entity_AdaptsToTheSaveFileFormat()
    {
        var sav = BlankSaveFile.Get(GameVersion.US);
        var (editor, _) = CreateEditor(sav);

        var gen2Aipom = CreateGen2Aipom();
        Assert.True(editor.LoadPKM(gen2Aipom));

        Assert.IsType<PK7>(editor.TargetPKM);
        Assert.Equal(sav.PKMType, editor.TargetPKM.GetType());
        Assert.Equal(Aipom, editor.TargetPKM.Species);
    }

    /// <summary>
    /// The reported symptom, asserted on the actual sprite bytes: after selecting Ambipom the preview
    /// must be the Ambipom asset, not the Ariados asset.
    /// </summary>
    [Fact]
    public void Issue234_AfterLoadingGen2Aipom_SelectingAmbipom_RendersAmbipomNotAriados()
    {
        var sav = BlankSaveFile.Get(GameVersion.US);
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);

        var (editor, renderer) = CreateEditor(sav);
        Assert.True(editor.LoadPKM(CreateGen2Aipom()));

        // Ambipom must actually be offered by the save-scoped Species dropdown, or the scenario
        // the reporter hit could not occur in the first place.
        Assert.Contains(editor.SpeciesList, x => x.Value == Ambipom);

        Normalize(editor);
        editor.Species = Ambipom;

        output.WriteLine($"VM.Species={editor.Species} Title='{editor.Title}' stored={editor.TargetPKM.Species} ({GameInfo.Strings.Species[editor.TargetPKM.Species]}) entity={editor.TargetPKM.GetType().Name}");

        // The reported symptom, asserted on the rendered sprite bytes.
        AssertRendersSpecies(editor, renderer, sav);

        // ...and the mechanism behind it: the stored entity must hold what the UI says it holds.
        Assert.Equal(Ambipom, editor.TargetPKM.Species);
        Assert.IsType<PK7>(editor.TargetPKM);
    }

    /// <summary>
    /// End-to-end through the surface named in the report: search the Encounter Database for Aipom on
    /// a save whose top results are Generation 2 encounters, load one into the editor and change the
    /// species, exactly as the reporter did.
    /// </summary>
    [Fact]
    public async Task Issue234_EncounterDatabase_AipomResult_ThenAmbipom_RendersAmbipom()
    {
        var sav = BlankSaveFile.Get(GameVersion.US);
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources);

        var (editor, renderer) = CreateEditor(sav);
        var db = new EncounterDatabaseViewModel(sav, renderer, Mock.Of<IDialogService>(), pk => editor.LoadPKM(pk));

        db.SelectedSpecies = Aipom;
        await db.SearchCommand.ExecuteAsync(null);
        Assert.NotEmpty(db.Results);

        // Prefer a wild slot over an egg encounter so the preview is a plain species render.
        var gen2Results = db.Results.Where(r => r.Encounter.Context == EntityContext.Gen2).ToList();
        var gen2 = gen2Results.Find(r => r.Encounter is EncounterSlot2) ?? gen2Results.FirstOrDefault();
        Assert.NotNull(gen2); // Gold/Silver/Crystal Aipom slots are reachable from a Gen 7 save
        output.WriteLine($"selected {gen2!.Encounter.GetType().Name} from {gen2.Encounter.Version}");

        await db.SelectEncounterCommand.ExecuteAsync(gen2);
        Assert.Equal(Aipom, editor.TargetPKM.Species);

        Normalize(editor);
        editor.Species = Ambipom;

        AssertRendersSpecies(editor, renderer, sav);
        Assert.Equal(Ambipom, editor.TargetPKM.Species);
        Assert.Equal(sav.PKMType, editor.TargetPKM.GetType());
    }

    /// <summary>
    /// The Encounter Database's own result rows render from each row's own entity, so a Gen 2 Aipom
    /// row shows Aipom. Guards against a future index-keyed/recycled sprite regression on that list.
    /// </summary>
    [Fact]
    public async Task EncounterDatabase_ResultRows_RenderTheirOwnSpecies()
    {
        var sav = BlankSaveFile.Get(GameVersion.US);
        var renderer = new AvaloniaSpriteRenderer(new AppSettings());
        renderer.Initialize(sav);

        var db = new EncounterDatabaseViewModel(sav, renderer, Mock.Of<IDialogService>(), _ => { });
        db.SelectedSpecies = Aipom;
        await db.SearchCommand.ExecuteAsync(null);
        Assert.NotEmpty(db.Results);

        var expected = RenderSpecies(renderer, sav, Aipom);
        var wrong = RenderSpecies(renderer, sav, Ariados);
        Assert.NotEqual(wrong, expected);

        foreach (var row in db.Results)
        {
            Assert.Equal(Aipom, row.Encounter.Species);
            Assert.NotNull(row.Sprite);
            Assert.NotEqual(wrong, row.Sprite);
        }
    }

    // ---------------------------------------------------------------------------------------

    private static (PokemonEditorViewModel Editor, ISpriteRenderer Renderer) CreateEditor(SaveFile sav)
    {
        var renderer = new AvaloniaSpriteRenderer(new AppSettings());
        renderer.Initialize(sav);
        var editor = new PokemonEditorViewModel(sav.BlankPKM, sav, renderer, Mock.Of<IDialogService>(), Mock.Of<IWindowService>());
        return (editor, renderer);
    }

    /// <summary>A Generation 2 wild Aipom, i.e. what the Encounter Database produces for a GSC slot.</summary>
    private static PK2 CreateGen2Aipom()
    {
        var pk = new PK2 { Species = Aipom, CurrentLevel = 18, OriginalTrainerName = "PKHEX", TID16 = 12345 };
        pk.Nickname = SpeciesName.GetSpeciesNameGeneration(Aipom, (int)LanguageID.English, 2);
        pk.ResetPartyStats();
        return pk;
    }

    /// <summary>
    /// Removes the sprite decorations (held-item corner badge, shiny overlay) so the preview is a
    /// plain species render and can be compared byte-for-byte against a reference render.
    /// </summary>
    private static void Normalize(PokemonEditorViewModel editor)
    {
        editor.IsShiny = false;
        editor.TargetPKM.HeldItem = 0;
    }

    private void AssertRendersSpecies(PokemonEditorViewModel editor, ISpriteRenderer renderer, SaveFile sav)
    {
        var ambipom = RenderSpecies(renderer, sav, Ambipom);
        var ariados = RenderSpecies(renderer, sav, Ariados);
        Assert.NotEqual(ariados, ambipom); // the two assets really are distinct

        var actual = editor.Sprite;
        Assert.NotNull(actual);
        output.WriteLine($"sprite bytes: actual={actual!.Length} ambipom={ambipom.Length} ariados={ariados.Length}");
        output.WriteLine($"matches Ambipom asset: {Describe(actual, ambipom)}; matches Ariados asset: {Describe(actual, ariados)}");

        Assert.NotEqual(ariados, actual);
        Assert.Equal(ambipom, actual);
    }

    /// <summary>Renders a bare species through the same pipeline the editor preview uses.</summary>
    private static byte[] RenderSpecies(ISpriteRenderer renderer, SaveFile sav, ushort species)
    {
        var pk = sav.BlankPKM;
        pk.Species = species;
        pk.Form = 0;
        pk.HeldItem = 0;
        pk.SetUnshiny();
        var sprite = renderer.GetSprite(pk);
        Assert.NotNull(sprite);
        return sprite!;
    }

    private static string Describe(byte[] actual, byte[] expected)
    {
        if (actual.AsSpan().SequenceEqual(expected))
            return "yes (identical bytes)";
        using var a = SKBitmap.Decode(actual);
        using var b = SKBitmap.Decode(expected);
        if (a is null || b is null)
            return "no (undecodable)";
        return $"no ({a.Width}x{a.Height} vs {b.Width}x{b.Height})";
    }
}
