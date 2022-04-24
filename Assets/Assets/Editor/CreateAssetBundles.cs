using System.Collections.Generic;
using System.IO;
using System.Linq;

using JetBrains.Annotations;

using UnityEditor;

using UnityEngine;

namespace Assets.Editor
{
    public static class CreateAssetBundles
    {
        [MenuItem("Assets/Rebuild Asset Bundles")]
        [UsedImplicitly]
        static void BuildBundles()
        {
            Debug.ClearDeveloperConsole();
            Debug.Log("Building Asset Bundles");

            Directory.CreateDirectory("AssetBundles");

            string[] allBundleNames = AssetDatabase.GetAllAssetBundleNames();
            List<string> bundleNames = allBundleNames.ToList().FindAll(name => name.EndsWith(".assetbundle"));
            List<(string bundleName, string bundleFile)> files = new List<(string, string)>();

            Debug.Log("Building " + bundleNames.Count  + " of " + allBundleNames.Length + " declared bundles");

            foreach (string bundleName in bundleNames)
            {
                Debug.Log("Building " + bundleName);

                BuildPipeline.BuildAssetBundles(
                    "AssetBundles",
                    new[] {
                        new AssetBundleBuild
                        {
                            assetBundleName = bundleName,
                            assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName),
                        }
                    },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.StandaloneWindows64
                );

                string bundleFile = Path.Combine("AssetBundles", bundleName);
                if (File.Exists(bundleFile))
                {
                    files.Add((bundleName, bundleFile));
                }
                else
                {
                    Debug.LogWarning("Building " + bundleName + " Failed");
                }
            }

            CreateAssetBundles.CopyFiles
            (
                files,
                Path.Combine("..", "Resources")
            );

            // We expect the dv directory to be junctioned under the project dir as "dv"
            string meshesDir = Path.Combine("..", "dv", "Mods", "DVLightSniper", "Assets", "Meshes");
            if (Directory.Exists(meshesDir))
            {
                CreateAssetBundles.CopyFiles
                (
                    from file in files where file.bundleName.StartsWith("meshes_") select file,
                    meshesDir
                );
            }
        }

        // Just done as a method to incorporate logging without it looking like a mess
        private static void CopyFiles(IEnumerable<(string, string)> files, string destinationPath)
        {
            foreach ((string bundleName, string bundleFile) in files)
            {
                Debug.Log("Copying " + bundleName + " to " + destinationPath);
                File.Copy(bundleFile, Path.Combine(destinationPath, bundleName), true);
            }
        }
    }
}