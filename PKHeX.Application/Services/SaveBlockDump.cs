using System.Globalization;
using System.Text;
using PKHeX.Core;

namespace PKHeX.Application.Services;

/// <summary>How a save file stores its blocks.</summary>
public enum SaveBlockSetKind
{
    /// <summary>The save has no block table the dump can enumerate.</summary>
    None,

    /// <summary>Hash-keyed <see cref="SCBlock"/> storage (Sword/Shield, Legends: Arceus, Scarlet/Violet, Legends: Z-A).</summary>
    SwishCrypto,

    /// <summary>Fixed, index-numbered <see cref="BlockInfo"/> regions inside the save buffer (Gen 5, Gen 6, Gen 7, Let's Go).</summary>
    Indexed,
}

/// <summary>
/// One block of a save, with its raw identity (hash key or block index) kept next to the readable name
/// that Core metadata resolves for it. Reads and writes go straight to the save's backing memory.
/// </summary>
public sealed class SaveBlockEntry
{
    private readonly SCBlock? _block;
    private readonly Memory<byte> _memory;

    internal SaveBlockEntry(SCBlock block, string? name)
    {
        _block = block;
        _memory = block.Raw;
        Kind = SaveBlockSetKind.SwishCrypto;
        Key = block.Key;
        KeyText = $"{block.Key:X8}";
        Name = name;
        Offset = -1;
        TypeName = block.Type.ToString();
        SubTypeName = block.Type == SCTypeCode.Array ? block.SubType.ToString() : null;
    }

    internal SaveBlockEntry(BlockInfo info, Memory<byte> memory, string? name)
    {
        _memory = memory;
        Kind = SaveBlockSetKind.Indexed;
        Key = info.ID;
        KeyText = $"{info.ID:00}";
        Name = name;
        Offset = info.Offset;
        TypeName = info.GetType().Name;
        SubTypeName = null;
    }

    public SaveBlockSetKind Kind { get; }

    /// <summary>SCBlock hash key, or the block index for indexed saves.</summary>
    public uint Key { get; }

    /// <summary>Raw identity as shown to the user: eight hex digits, or the two-digit block index.</summary>
    public string KeyText { get; }

    /// <summary>Readable name resolved from Core metadata; <see langword="null"/> when the block is unknown.</summary>
    public string? Name { get; }

    public string TypeName { get; }

    /// <summary>Element type of an array block; <see langword="null"/> for every other block.</summary>
    public string? SubTypeName { get; }

    /// <summary>Offset inside the save buffer; -1 for hash-keyed blocks.</summary>
    public int Offset { get; }

    public int Length => _memory.Length;

    /// <summary>A block without backing bytes (a boolean flag) cannot be exported or imported as data.</summary>
    public bool HasData => Length != 0;

    /// <summary>Single stored value (scalars and booleans) in invariant culture; <see langword="null"/> otherwise.</summary>
    public string? ValueText
    {
        get
        {
            if (_block is null)
                return null;
            if (_block.Type is SCTypeCode.Bool1)
                return bool.FalseString;
            if (_block.Type is SCTypeCode.Bool2)
                return bool.TrueString;
            if (!_block.HasValue())
                return null;
            return Convert.ToString(_block.GetValue(), CultureInfo.InvariantCulture);
        }
    }

    public ReadOnlySpan<byte> Data => _memory.Span;

    internal SCBlock? Block => _block;

    /// <summary>File name used for folder exports; folder import parses the identity back from it.</summary>
    public string ExportFileName
    {
        get
        {
            if (_block is not null)
                return SanitizeFileName(SCBlockUtil.GetBlockFileNameWithoutExtension(_block)) + ".bin";
            return Name is null ? $"{KeyText}.bin" : $"{KeyText} {SanitizeFileName(Name)}.bin";
        }
    }

    /// <summary>Replaces the block bytes. The size must match exactly; nothing is written otherwise.</summary>
    public void Replace(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length || !HasData)
            throw new InvalidOperationException($"Block {KeyText} holds {Length} bytes; the data has {data.Length} bytes.");
        if (_block is not null)
            _block.ChangeData(data);
        else
            data.CopyTo(_memory.Span);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
            builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return builder.ToString();
    }
}

/// <summary>
/// The enumerated blocks of one save file, with names resolved through Core metadata
/// (<see cref="SCBlockMetadata"/> for hash-keyed saves, the accessor's named <see cref="IDataIndirect"/>
/// properties for indexed saves). This mirrors upstream PKHeX's "Savedata Block Dump".
/// </summary>
public sealed class SaveBlockSet
{
    private readonly SaveFile? _save;
    private readonly Dictionary<uint, SaveBlockEntry> _byKey;

    private SaveBlockSet(SaveFile? save, SaveBlockSetKind kind, IReadOnlyList<SaveBlockEntry> entries)
    {
        _save = save;
        Kind = kind;
        Entries = entries;
        _byKey = new Dictionary<uint, SaveBlockEntry>(entries.Count);
        foreach (var entry in entries)
            _byKey.TryAdd(entry.Key, entry);
    }

    public SaveBlockSetKind Kind { get; }
    public IReadOnlyList<SaveBlockEntry> Entries { get; }
    public bool IsSupported => Kind != SaveBlockSetKind.None;
    public Type? SaveType => _save?.GetType();

    public SaveBlockEntry? Find(uint key) => _byKey.GetValueOrDefault(key);

    /// <summary>Builds the block set for <paramref name="save"/>; unsupported saves give an empty set.</summary>
    public static SaveBlockSet Create(SaveFile save)
    {
        if (save is ISCBlockArray sc)
            return CreateSwish(save, sc);
        if (TryGetIndexed(save, out var accessor, out var infos))
            return CreateIndexed(save, accessor, infos);
        return new SaveBlockSet(save, SaveBlockSetKind.None, []);
    }

    private static SaveBlockSet CreateSwish(SaveFile save, ISCBlockArray sc)
    {
        var metadata = new SCBlockMetadata(sc.Accessor, []);
        var entries = new List<SaveBlockEntry>(sc.AllBlocks.Count);
        foreach (var block in sc.AllBlocks)
            entries.Add(new SaveBlockEntry(block, metadata.GetBlockName(block, out _)));
        return new SaveBlockSet(save, SaveBlockSetKind.SwishCrypto, entries);
    }

    private static SaveBlockSet CreateIndexed(SaveFile save, object accessor, IReadOnlyList<BlockInfo> infos)
    {
        // Same reflection-based Core helper that SaveBlockMetadata<T> uses for upstream's accessor view.
        var named = accessor.GetType().GetAllPropertiesOfType<IDataIndirect>(accessor);
        var buffer = save.Buffer;
        var entries = new List<SaveBlockEntry>(infos.Count);
        foreach (var info in infos)
        {
            var memory = buffer.Slice(info.Offset, info.Length);
            entries.Add(new SaveBlockEntry(info, memory, ResolveIndexedName(named, memory.Span)));
        }
        return new SaveBlockSet(save, SaveBlockSetKind.Indexed, entries);
    }

    /// <summary>
    /// A named accessor property belongs to an indexed block when its bytes are the same memory as the block,
    /// or sit inside it. An exact match wins; otherwise every contained name is listed in storage order.
    /// </summary>
    private static string? ResolveIndexedName(Dictionary<string, IDataIndirect> named, ReadOnlySpan<byte> block)
    {
        if (block.IsEmpty)
            return null;
        List<(int Offset, string Name)>? contained = null;
        foreach (var (name, value) in named)
        {
            ReadOnlySpan<byte> data = value.Data;
            if (data.IsEmpty || !block.Overlaps(data, out var offset) || offset < 0 || offset + data.Length > block.Length)
                continue;
            if (offset == 0 && data.Length == block.Length)
                return name;
            (contained ??= []).Add((offset, name));
        }
        if (contained is null)
            return null;
        contained.Sort((a, b) => a.Offset != b.Offset ? a.Offset.CompareTo(b.Offset) : string.CompareOrdinal(a.Name, b.Name));
        return string.Join(", ", contained.Select(z => z.Name));
    }

    private static bool TryGetIndexed(SaveFile save, out object accessor, out IReadOnlyList<BlockInfo> infos)
    {
        (accessor, infos) = save switch
        {
            SAV5BW s => ((object)s.Blocks, s.AllBlocks),
            SAV5B2W2 s => (s.Blocks, s.AllBlocks),
            SAV6XY s => (s.Blocks, s.AllBlocks),
            SAV6AO s => (s.Blocks, s.AllBlocks),
            SAV6AODemo s => (s.Blocks, s.AllBlocks),
            SAV7SM s => (s.Blocks, s.AllBlocks),
            SAV7USUM s => (s.Blocks, s.AllBlocks),
            SAV7b s => (s.Blocks, s.AllBlocks),
            _ => (null!, []),
        };
        return accessor is not null;
    }

    /// <summary>
    /// Every block in one file. Hash-keyed saves use Core's <see cref="SCBlockUtil.ExportAllBlocks"/> with the
    /// requested options; indexed saves concatenate the block regions in table order.
    /// </summary>
    public byte[] ExportAll(SCBlockExportOption option)
    {
        if (Kind == SaveBlockSetKind.SwishCrypto)
            return SCBlockUtil.ExportAllBlocks(Entries.Select(z => z.Block!).ToArray(), option);

        using var stream = new MemoryStream();
        foreach (var entry in Entries)
            stream.Write(entry.Data);
        return stream.ToArray();
    }

    /// <summary>Writes one file per block that has data. Returns the number of files written.</summary>
    public int ExportToFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        var count = 0;
        foreach (var entry in Entries)
        {
            if (!entry.HasData)
                continue;
            File.WriteAllBytes(Path.Combine(folder, entry.ExportFileName), entry.Data.ToArray());
            count++;
        }
        return count;
    }

    /// <summary>
    /// Reads the block identity from a dump file name: the first token must be exactly eight hex digits
    /// (hash-keyed) or a decimal block index (indexed). Loose parsing is deliberately refused so that a
    /// file can never be routed to a block it does not belong to.
    /// </summary>
    public bool TryParseFileKey(string fileName, out uint key)
    {
        key = 0;
        var stem = Path.GetFileNameWithoutExtension(fileName).AsSpan();
        var space = stem.IndexOf(' ');
        if (space >= 0)
            stem = stem[..space];
        return Kind switch
        {
            SaveBlockSetKind.SwishCrypto => stem.Length == 8 && uint.TryParse(stem, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out key),
            SaveBlockSetKind.Indexed => stem.Length is > 0 and <= 3 && uint.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out key),
            _ => false,
        };
    }

    /// <summary>
    /// Reads a folder dump and validates every file against the live block table without writing anything.
    /// Call <see cref="SaveBlockImportPlan.Apply"/> after the user confirms.
    /// </summary>
    public SaveBlockImportPlan PlanFolderImport(string folder)
    {
        var accepted = new Dictionary<uint, SaveBlockImportItem>();
        var duplicates = new HashSet<uint>();
        var failures = new List<SaveBlockImportFailure>();
        foreach (var path in Directory.EnumerateFiles(folder).Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            if (!TryParseFileKey(fileName, out var key))
            {
                failures.Add(new(fileName, SaveBlockImportFailureReason.UnrecognizedName));
                continue;
            }
            if (Find(key) is not { } entry)
            {
                failures.Add(new(fileName, SaveBlockImportFailureReason.UnknownBlock));
                continue;
            }
            if (!entry.HasData)
            {
                failures.Add(new(fileName, SaveBlockImportFailureReason.NoData));
                continue;
            }
            long size;
            try { size = new FileInfo(path).Length; }
            catch (Exception) { failures.Add(new(fileName, SaveBlockImportFailureReason.ReadError)); continue; }
            if (size != entry.Length)
            {
                failures.Add(new(fileName, SaveBlockImportFailureReason.SizeMismatch, entry.Length, size));
                continue;
            }
            if (duplicates.Contains(key) || accepted.ContainsKey(key))
            {
                // Two files claim the same block: refuse both rather than guess which one is meant.
                if (accepted.Remove(key, out var first))
                    failures.Add(new(first.FileName, SaveBlockImportFailureReason.Duplicate));
                duplicates.Add(key);
                failures.Add(new(fileName, SaveBlockImportFailureReason.Duplicate));
                continue;
            }
            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception) { failures.Add(new(fileName, SaveBlockImportFailureReason.ReadError)); continue; }
            if (data.Length != entry.Length)
            {
                failures.Add(new(fileName, SaveBlockImportFailureReason.SizeMismatch, entry.Length, data.Length));
                continue;
            }
            accepted.Add(key, new SaveBlockImportItem(entry, fileName, data));
        }
        var items = accepted.Values.Where(z => !z.Entry.Data.SequenceEqual(z.Data)).ToList();
        var unchanged = accepted.Count - items.Count;
        return new SaveBlockImportPlan(_save, items, failures, unchanged);
    }

    /// <summary>Both sets must come from the same save type; otherwise keys and indexes mean different things.</summary>
    public static bool AreComparable(SaveBlockSet before, SaveBlockSet after)
        => before.IsSupported && before.Kind == after.Kind && before.SaveType == after.SaveType;

    /// <summary>
    /// Compares two block sets of the same save format: blocks present in only one side, and blocks whose
    /// type, length or bytes differ. Raw keys are always reported; names come from either side's metadata.
    /// </summary>
    public static IReadOnlyList<SaveBlockDifference> Compare(SaveBlockSet before, SaveBlockSet after)
    {
        if (!AreComparable(before, after))
            throw new ArgumentException("The two saves do not use the same block format.", nameof(after));

        var result = new List<SaveBlockDifference>();
        foreach (var old in before.Entries)
        {
            var current = after.Find(old.Key);
            if (current is null)
            {
                result.Add(new(SaveBlockDifferenceKind.Removed, old.KeyText, old.Name, old.TypeName, null, old.Length, 0, null, null, 0));
                continue;
            }
            var name = old.Name ?? current.Name;
            if (old.TypeName != current.TypeName)
            {
                // Bool1 <-> Bool2 is how hash-keyed saves store a flag change; report it as a value change.
                var isFlag = old.Block?.Type.IsBoolean() == true && current.Block?.Type.IsBoolean() == true;
                result.Add(isFlag
                    ? new(SaveBlockDifferenceKind.ValueChanged, old.KeyText, name, old.TypeName, current.TypeName, old.Length, current.Length, old.ValueText, current.ValueText, 0)
                    : new(SaveBlockDifferenceKind.TypeChanged, old.KeyText, name, old.TypeName, current.TypeName, old.Length, current.Length, null, null, 0));
                continue;
            }
            if (old.Length != current.Length)
            {
                result.Add(new(SaveBlockDifferenceKind.LengthChanged, old.KeyText, name, old.TypeName, current.TypeName, old.Length, current.Length, null, null, 0));
                continue;
            }
            var changed = CountChangedBytes(old.Data, current.Data);
            if (changed == 0)
                continue;
            var oldValue = old.ValueText;
            var kind = oldValue is null ? SaveBlockDifferenceKind.BytesChanged : SaveBlockDifferenceKind.ValueChanged;
            result.Add(new(kind, old.KeyText, name, old.TypeName, current.TypeName, old.Length, current.Length, oldValue, current.ValueText, changed));
        }
        foreach (var added in after.Entries)
        {
            if (before.Find(added.Key) is null)
                result.Add(new(SaveBlockDifferenceKind.Added, added.KeyText, added.Name, null, added.TypeName, 0, added.Length, null, null, 0));
        }
        return result;
    }

    private static int CountChangedBytes(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var count = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                count++;
        }
        return count;
    }
}

public enum SaveBlockDifferenceKind
{
    Added,
    Removed,
    TypeChanged,
    LengthChanged,
    ValueChanged,
    BytesChanged,
}

/// <summary>One difference between two block sets.</summary>
public sealed record SaveBlockDifference(
    SaveBlockDifferenceKind Kind,
    string KeyText,
    string? Name,
    string? OldType,
    string? NewType,
    int OldLength,
    int NewLength,
    string? OldValue,
    string? NewValue,
    int ChangedBytes);

public enum SaveBlockImportFailureReason
{
    /// <summary>The file name does not start with a block identity.</summary>
    UnrecognizedName,
    /// <summary>The save has no block with that identity.</summary>
    UnknownBlock,
    /// <summary>The block is a flag with no bytes.</summary>
    NoData,
    /// <summary>The file size differs from the block size.</summary>
    SizeMismatch,
    /// <summary>More than one file names the same block.</summary>
    Duplicate,
    /// <summary>The file could not be read.</summary>
    ReadError,
}

public sealed record SaveBlockImportItem(SaveBlockEntry Entry, string FileName, byte[] Data);

public sealed record SaveBlockImportFailure(string FileName, SaveBlockImportFailureReason Reason, int ExpectedLength = 0, long ActualLength = 0);

/// <summary>
/// A validated, not-yet-applied folder import. Nothing touches the save until <see cref="Apply"/>.
/// </summary>
public sealed class SaveBlockImportPlan
{
    private readonly SaveFile? _save;
    private bool _applied;

    internal SaveBlockImportPlan(SaveFile? save, IReadOnlyList<SaveBlockImportItem> items, IReadOnlyList<SaveBlockImportFailure> failures, int unchangedCount)
    {
        _save = save;
        Items = items;
        Failures = failures;
        UnchangedCount = unchangedCount;
    }

    /// <summary>Validated files whose bytes differ from the current block.</summary>
    public IReadOnlyList<SaveBlockImportItem> Items { get; }

    /// <summary>Files that were refused, with the reason.</summary>
    public IReadOnlyList<SaveBlockImportFailure> Failures { get; }

    /// <summary>Validated files that already match the block bytes.</summary>
    public int UnchangedCount { get; }

    /// <summary>Writes every validated item. Each write re-checks the size. Returns the number of blocks changed.</summary>
    public int Apply()
    {
        if (_applied)
            throw new InvalidOperationException("The import plan was already applied.");
        _applied = true;
        foreach (var item in Items)
            item.Entry.Replace(item.Data);
        if (Items.Count != 0 && _save is not null)
            _save.State.Edited = true;
        return Items.Count;
    }
}

/// <summary>Formats a bounded hex and ASCII preview of block bytes.</summary>
public static class HexPreview
{
    /// <summary>Largest number of bytes the preview renders; bigger blocks are truncated.</summary>
    public const int MaxBytes = 0x1000;

    public static string Format(ReadOnlySpan<byte> data, int bytesPerRow = 16, int maxBytes = MaxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytesPerRow, 1);
        var length = Math.Min(data.Length, maxBytes);
        var half = bytesPerRow / 2;
        var builder = new StringBuilder((length / bytesPerRow + 1) * (12 + (bytesPerRow * 4)));
        for (var row = 0; row < length; row += bytesPerRow)
        {
            if (row != 0)
                builder.Append('\n');
            builder.Append(row.ToString("X8", CultureInfo.InvariantCulture)).Append("  ");
            var count = Math.Min(bytesPerRow, length - row);
            for (var i = 0; i < bytesPerRow; i++)
            {
                if (i == half && half != 0)
                    builder.Append(' ');
                if (i < count)
                    builder.Append(data[row + i].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                else
                    builder.Append("   ");
            }
            builder.Append(' ');
            for (var i = 0; i < count; i++)
            {
                var b = data[row + i];
                builder.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }
        }
        return builder.ToString();
    }
}
