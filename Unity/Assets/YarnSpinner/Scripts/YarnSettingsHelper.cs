using System;
using System.IO;
using UnityEngine;

public static class YarnSettingsHelper 
{

    /// <summary>
    /// Read the user's language preferences from disk.
    /// </summary>
    public static void ReadPreferencesFromDisk<T>(T settingsClass, string storagePath) {
        // Check file's existence
        bool fileExists = File.Exists(storagePath);
        if (!fileExists) {
            // Wen don't throw an error since during OnEnable() all values will be initialized with 
            // the system's default and create a new file once this class get's out of scope
            Debug.Log("No previous Yarn Spinner preferences have been found.");
            return;
        }

        // Load file into memory
        string jsonString;
        try {
            jsonString = File.ReadAllText(storagePath);
        } catch (Exception) {
            // No big deal since we'll initialize all values during OnEnable()
            Debug.Log("Error loading Yarn Spinner preferences from JSON.");
            return;
        }

        // Parse json to *this* ScriptableObject
        try {
            JsonUtility.FromJsonOverwrite(jsonString, settingsClass);
        } catch (Exception) {
            // No big deal since we'll initialize all values during OnEnable()
            Debug.Log("Error parsing Yarn Spinner preferences from JSON.");
            return;
        }
    }

    /// <summary>
    /// Save the user's language preferences to disk.
    /// </summary>
    public static void WritePreferencesToDisk<T>(T settingsClass, string storagePath) {
        string settingsJson = JsonUtility.ToJson(settingsClass, true);
        try {
            File.WriteAllText(storagePath, settingsJson);
        } catch (Exception) {
            Debug.LogError("Saving Yarn Spinner preferences to disk failed!");
        }
    }
}
