namespace PKHeX.Presentation.ViewModels;

/// <summary>Top-level user task shown by the main shell.</summary>
public enum MainWorkspace
{
    /// <summary>Pokémon editing with storage and party context.</summary>
    Pokemon,

    /// <summary>Save-level editors such as Trainer, Inventory, Events, Gifts, and Batch.</summary>
    Save,

    /// <summary>Search, reports, and database tools.</summary>
    Reports,
}
