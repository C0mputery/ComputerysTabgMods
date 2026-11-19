using System.IO;
using System.Reflection;
using UnityEngine;

namespace ComputerysBetterShop;

public static class EmbeddedAssetBundle {
    private static AssetBundle _assetBundle;
    
    private static void LoadAssetBundleFromResources() {
        const string assetBundleResourcePath = "ComputerysBetterShop.Resources.AssetBundle";
        
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream manifestResourceStream = assembly.GetManifestResourceStream(assetBundleResourcePath)!;
        _assetBundle = AssetBundle.LoadFromStream(manifestResourceStream)!;
    }
    
    internal static void Initialize() { LoadAssetBundleFromResources(); }
    
    public static T LoadAsset<T>(string assetName) where T : Object { return _assetBundle.LoadAsset<T>(assetName); }
}