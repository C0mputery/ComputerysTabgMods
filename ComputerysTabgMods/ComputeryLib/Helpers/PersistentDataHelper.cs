using System.IO;
using BepInEx;

namespace ComputeryLib.Helpers;

internal static class PersistentDataHelper {
    internal static readonly string PersistentDataPath = Path.Combine(Paths.GameRootPath, "ComputeryLibPersistentData");
    
    static PersistentDataHelper() {
        if (!Directory.Exists(PersistentDataPath)) { Directory.CreateDirectory(PersistentDataPath); }
    }
}