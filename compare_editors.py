
import os
import glob

# WinForms: PKHeX.WinForms/Subforms/**/SAV_*.cs
# Avalonia: PKHeX.Avalonia/ViewModels/**/*EditorViewModel.cs

winforms_path = "PKHeX.WinForms/Subforms"
avalonia_path = "PKHeX.Avalonia/ViewModels"

def get_winforms_editors():
    editors = set()
    for root, dirs, files in os.walk(winforms_path):
        for file in files:
            if file.startswith("SAV_") and file.endswith(".cs") and not file.endswith(".Designer.cs"):
                # SAV_{Name}.cs
                name = file[4:-3] 
                editors.add(name)
    return editors

def get_avalonia_editors():
    editors = set()
    for root, dirs, files in os.walk(avalonia_path):
        for file in files:
            if file.endswith("EditorViewModel.cs"):
                # {Name}EditorViewModel.cs
                name = file[:-16]
                editors.add(name)
            elif file.endswith("ViewerViewModel.cs"):
                # {Name}ViewerViewModel.cs
                name = file[:-16]
                editors.add(name)
            elif file.endswith("ListViewModel.cs"):
                 # {Name}ListViewModel.cs
                 name = file[:-14]
                 editors.add(name)
            elif file == "MysteryGiftDatabaseViewModel.cs":
                 editors.add("MysteryGiftDatabase")
    return editors

wf = get_winforms_editors()
av = get_avalonia_editors()

print(f"WinForms Count: {len(wf)}")
print(f"Avalonia Count: {len(av)}")

print("\n--- Miss in Avalonia (Present in WinForms) ---")
for x in sorted(wf - av):
    print(x)

print("\n--- Extra in Avalonia (Not in WinForms SAV_*) ---")
for x in sorted(av - wf):
    print(x)

# Fuzzy match check (handle naming differences)
manual_mappings = {
    "BerryFieldXY": "BerryFieldEd",
    "BlockDump8": "BlockEd",
    "BoxViewer": "BoxViewer",
    "Donut9a": "DonutEd",
    "FlagWork8b": "EventFlagsEd", # Assuming shared
    "FlagWork9a": "EventFlagsEd", # Assuming shared
    "FolderList": "FolderList", # confirm if missing
    "GroupViewer": "GroupViewer", # confirm if missing
    "MysteryGiftDB": "MysteryGiftDatabase",
    "PokeBlockORAS": "PokeBlockEd",
    "Pokedex9a": "PokedexGen9Ed",
    "PokedexBDSP": "Pokedex8bEd", # Check this
    "PokedexGG": "Pokedex7bEd", # Check this
    "PokedexORAS": "Pokedex6Ed", 
    "PokedexResearchEditorLA": "PokedexLAEd",
    "PokedexSM": "Pokedex7Ed",
    "PokedexSV": "PokedexGen9Ed",
    "PokedexSVKitakami": "PokedexGen9Ed",
    "PokedexSWSH": "Pokedex8Ed",
    "PokedexXY": "Pokedex6Ed", # Check this
    "Raid8": "RaidEd",
    "SimplePokedex": "PokedexSimpleEd",
    "Trainer4BR": "GearBREd", # Maybe? OR SimpleTrainerEd
    "Trainer7": "TrainerEd",
    "Trainer7GG": "TrainerEd",
    "Trainer8": "TrainerEd",
    "Trainer8a": "TrainerEd",
    "Trainer8b": "TrainerEd",
    "Trainer9": "TrainerEd",
    "Trainer9a": "TrainerEd",
    "Wondercard": "MysteryGiftEd", # Integrated into MysteryGiftEditorViewModel
    "BoxViewer": "BoxViewer", # Exists
    "MysteryGiftDB": "MysteryGiftDatabase", # Exists
    "GroupViewer": "GroupViewer", # To Implement
    "FolderList": "FolderList", # To Implement
}

missing = wf - av
extras = av - wf
resolved = set()


print("\n--- Manual Mappings ---")
for m_wf, m_av in manual_mappings.items():
    if m_wf in missing:
        # Check if matched in extras
        # The keys in 'extras' have 'Ed' suffix (e.g. 'ApricornEd')
        # We need to see if m_av (e.g. 'BerryFieldEd') is in extras
        if m_av in extras:
            print(f"{m_wf} <--> {m_av} (Manual)")
            resolved.add(m_wf)
        # Also check fuzzy cases
        else:
             # loose check
             for e in extras:
                 if m_av.lower() == e.lower():
                     print(f"{m_wf} <--> {e} (Manual Fuzzy)")
                     resolved.add(m_wf)
                     break



print("\n--- Likely Mismatches (Renamed) ---")
for m in missing:
    for e in extras:
        if m.lower() in e.lower() or e.lower() in m.lower():
            print(f"{m} <--> {e}")
            resolved.add(m)

print("\n--- Truly Missing (Unresolved) ---")
for m in sorted(missing - resolved):
    print(m)
