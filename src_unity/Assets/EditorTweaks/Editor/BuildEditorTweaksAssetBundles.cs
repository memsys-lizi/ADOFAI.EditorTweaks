using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildEditorTweaksAssetBundles
{
    private const string ToolbarBundleName = "editortweaks_toolbar";
    private const string ToolbarAssetRoot = "Assets/EditorTweaks/Toolbar";

    [MenuItem("EditorTweaks/Build AssetBundles")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void Build()
    {
        AssignToolbarBundleName();

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string repositoryRoot = Directory.GetParent(projectRoot)!.FullName;
        string outputDirectory = Path.Combine(repositoryRoot, "Resources", "AssetBundles");
        Directory.CreateDirectory(outputDirectory);

        BuildPipeline.BuildAssetBundles(
            outputDirectory,
            BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget);

        AssetDatabase.Refresh();
        Debug.Log($"EditorTweaks AssetBundles built to: {outputDirectory}");
    }

    private static void AssignToolbarBundleName()
    {
        string[] assetGuids = AssetDatabase.FindAssets(string.Empty, new[] { ToolbarAssetRoot });
        foreach (string guid in assetGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)
                || path.EndsWith(".cs")
                || path.EndsWith("toolbar_icons_preview.png"))
            {
                continue;
            }

            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                if (importer is TextureImporter textureImporter)
                {
                    textureImporter.textureType = TextureImporterType.Sprite;
                    textureImporter.spriteImportMode = SpriteImportMode.Single;
                    textureImporter.alphaIsTransparency = true;
                    textureImporter.mipmapEnabled = false;
                    textureImporter.SaveAndReimport();
                }

                importer.assetBundleName = ToolbarBundleName;
            }
        }

        AssetDatabase.RemoveUnusedAssetBundleNames();
        AssetDatabase.SaveAssets();
    }
}
