using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Contains utility methods for working with Yarn Spinner content in the
/// Unity Editor.
/// </summary>
public static class YarnEditorUtility {

    // GUID for editor assets. (Doing it like this means that we don't have
    // to worry about where the assets are on disk, if the user has moved
    // Yarn Spinner around.)
    const string IconTextureGUID = "528a6dd601766934abb8b1053bd798ef";

    /// <summary>
    /// Returns a <see cref="Texture2D"/> that can be used to represent
    /// Yarn files.
    /// </summary>
    /// <returns>A texture to use in the Unity editor for Yarn
    /// files.</returns>
    public static Texture2D GetYarnDocumentIconTexture()
    {
        string textureAssetPath = AssetDatabase.GUIDToAssetPath(IconTextureGUID);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
    }

}
