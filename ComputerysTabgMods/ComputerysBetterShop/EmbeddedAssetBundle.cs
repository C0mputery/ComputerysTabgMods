using System.IO;
using System.Reflection;
using UnityEngine;

namespace ComputerysBetterShop;

public static class EmbeddedAssetBundle {
    private const string AssetBundleResourcePath = "ComputerysBetterShop.Resources.AssetBundle";
    private static AssetBundle _assetBundle;
    
    private static void LoadAssetBundleFromResources() {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream manifestResourceStream = assembly.GetManifestResourceStream(AssetBundleResourcePath)!;
        _assetBundle = AssetBundle.LoadFromStream(manifestResourceStream)!;
    }
    
    internal static void Initialize() { LoadAssetBundleFromResources(); }
    
    public static T LoadAsset<T>(string assetName) where T : Object { return _assetBundle.LoadAsset<T>(assetName); }
}