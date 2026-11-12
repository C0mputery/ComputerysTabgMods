using System.IO;
using BepInEx;

namespace ComputeryLib.Utilities;

internal static class PersistantDataUtility {
    internal static readonly string PersistentDataPath = Path.Combine(Paths.GameRootPath, "ComputeryLibPersistentData");
    
    static PersistantDataUtility() {
        if (!Directory.Exists(PersistentDataPath)) { Directory.CreateDirectory(PersistentDataPath); }
    }
}