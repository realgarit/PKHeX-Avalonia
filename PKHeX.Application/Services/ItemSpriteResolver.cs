using PKHeX.Core;

namespace PKHeX.Application.Services;

public enum ItemSpriteKind { None, Item, Machine, Record, Unknown }

public sealed record ItemSpriteReference(ItemSpriteKind Kind, IReadOnlyList<int> ItemIds);

/// <summary>Resolves bag IDs to modern item identities without modifying saved item IDs.</summary>
public static class ItemSpriteResolver
{
    // Raw, invariant resources: display strings may be localized or have game-specific suffixes.
    private static readonly string[] Modern = ReadNames("Items");
    private static readonly string[] Gen1 = ReadNames("ItemsG1");
    private static readonly string[] Gen2 = ReadNames("ItemsG2");
    private static readonly string[] Gen3 = ReadNames("ItemsG3");
    private static readonly string[] Colosseum = GetGameCubeNames("ItemsG3Colosseum");
    private static readonly string[] XD = GetGameCubeNames("ItemsG3XD");

    public static ItemSpriteReference Resolve(int itemId, EntityContext context, GameVersion version)
    {
        if (itemId == 0)
            return new(ItemSpriteKind.None, []);
        var names = context switch
        {
            EntityContext.Gen1 => Gen1,
            EntityContext.Gen2 => Gen2,
            EntityContext.Gen3 when version == GameVersion.COLO => Colosseum,
            EntityContext.Gen3 when version == GameVersion.XD => XD,
            EntityContext.Gen3 => Gen3,
            _ => Modern,
        };
        if ((uint)itemId >= names.Length || string.IsNullOrWhiteSpace(names[itemId]) || names[itemId].Contains('?'))
            return new(ItemSpriteKind.Unknown, []);

        var name = names[itemId];
        if (IsMachine(name, "TR"))
            return new(ItemSpriteKind.Record, []);
        if (IsMachine(name, "TM") || IsMachine(name, "HM"))
            return new(ItemSpriteKind.Machine, []);

        var ids = new List<int>();
        if (context == EntityContext.Gen2 && itemId <= byte.MaxValue)
            AddConverted(ItemConverter.GetItemFuture2((byte)itemId));
        else if (context == EntityContext.Gen3 && itemId < Gen3.Length)
            AddConverted(ItemConverter.GetItemFuture3((ushort)itemId));
        else if (context is not (EntityContext.Gen1 or EntityContext.Gen2 or EntityContext.Gen3))
            ids.Add(itemId);

        // Legacy names and duplicate modern identities can reuse an existing image.
        name = name switch
        {
            "Bicycle" => "Bike",
            "Parlyz Heal" => "Paralyze Heal",
            "X Defend" => "X Defense",
            "X Special" => "X Sp. Atk",
            "Exp. All" => "Exp. Share",
            "Itemfinder" => "Dowsing Machine",
            "Oak's Parcel" => "Parcel",
            _ => name,
        };
        for (int i = 1; i < Modern.Length; i++)
        {
            if (Modern[i] == name && !ids.Contains(i))
                ids.Add(i);
        }
        return new(ids.Count == 0 ? ItemSpriteKind.Unknown : ItemSpriteKind.Item, ids);

        void AddConverted(int id)
        {
            // Core uses 128 as its unavailable-conversion sentinel, not an item identity.
            if (id != 128 && id > 0)
                ids.Add(id);
        }
    }

    private static bool IsMachine(string name, string prefix)
        => name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > 2 && char.IsAsciiDigit(name[2]);

    private static string[] GetGameCubeNames(string resource)
    {
        var extra = ReadNames(resource);
        var names = new string[500 + extra.Length];
        Gen3.CopyTo(names, 0);
        extra.CopyTo(names, 500);
        return names;
    }

    // GameStrings decorates Util.CachedStrings in place. Read the embedded text directly so
    // identity matching is independent of display-language initialization and duplicate labels.
    private static string[] ReadNames(string resource)
        => Util.GetStringResource($"text_{resource}_en").Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
}
