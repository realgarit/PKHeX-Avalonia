
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
missing = wf - av
extras = av - wf
resolved = set()

print("\n--- Likely Mismatches (Renamed) ---")
for m in missing:
    for e in extras:
        if m.lower() in e.lower() or e.lower() in m.lower():
            print(f"{m} <--> {e}")
            resolved.add(m)

print("\n--- Truly Missing (Unresolved) ---")
for m in sorted(missing - resolved):
    print(m)
