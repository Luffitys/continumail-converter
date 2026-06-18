// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

var pstPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "template.pst");

if (File.Exists(pstPath))
{
    File.Delete(pstPath);
}

Console.WriteLine($"Creating blank Unicode PST at: {pstPath}");

Type outlookType = Type.GetTypeFromProgID("Outlook.Application");
dynamic outlook = Activator.CreateInstance(outlookType);
dynamic ns = outlook.GetNamespace("MAPI");

const int olStoreUnicode = 3; // OlStoreType.olStoreUnicode

// Adds a new PST data file to the current profile, creating it on disk if it doesn't exist.
ns.AddStoreEx(pstPath, olStoreUnicode);

dynamic newStore = null;
foreach (dynamic store in ns.Stores)
{
    string filePath = null;
    try { filePath = store.FilePath; } catch { }
    if (string.Equals(filePath, pstPath, StringComparison.OrdinalIgnoreCase))
    {
        newStore = store;
        break;
    }
}

if (newStore == null)
{
    throw new System.Exception("Could not locate the newly created PST store in the profile.");
}

dynamic root = newStore.GetRootFolder();
Console.WriteLine($"Root folder: {root.Name}");

// Remove the store from the profile again - we only wanted the file on disk.
ns.RemoveStore(root);

Marshal.ReleaseComObject(ns);
Marshal.ReleaseComObject(outlook);

Console.WriteLine("Done. PST file created at: " + pstPath);
