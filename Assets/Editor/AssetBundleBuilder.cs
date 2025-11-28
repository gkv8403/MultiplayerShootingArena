using UnityEditor;
using System.IO;
using UnityEngine;
public class AssetBundleBuilder
{
    [MenuItem("Assets/Build AssetBundles")]
    public static void BuildAllAssetBundles()
    {
        string outputPath = "Assets/AssetBundles";

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        BuildPipeline.BuildAssetBundles(outputPath,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows); // Change as needed (Android/iOS/etc)

        Debug.Log("AssetBundles Built Successfully!");
    }
}
