using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Provides methods for loading and saving a <see cref="ScriptableObject"/> class used for settings.
/// </summary>
public static class YarnSettingsHelper {
    /// <summary>
    /// Read the user's language preferences from disk.
    /// </summary>
    public static void ReadPreferencesFromDisk<T>(T settingsClass, string storagePath, Action OnReadError = null) {
        // Check file's existence
        bool fileExists = File.Exists(storagePath);
        if (!fileExists) {
            // Wen don't throw an error since during OnEnable() all values will be initialized with 
            // the system's default and create a new file once this class get's out of scope
            Debug.LogFormat("No previous Yarn Spinner preferences have been found in {0}.", storagePath);
            OnReadError?.Invoke();
            return;
        }

        // Load file into memory
        string jsonString;
        try {
            jsonString = File.ReadAllText(storagePath);
        } catch (Exception) {
            // No big deal since we'll initialize all values during OnEnable()
            Debug.Log("Error loading Yarn Spinner preferences from JSON.");
            OnReadError?.Invoke();
            return;
        }

        ReadJsonFromString(settingsClass, jsonString, OnReadError);
    }

    /// <summary>
    /// Creates a class from a JSON string representing it's state.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="settingsClass">The type of the class to be created.</param>
    /// <param name="json">The state of the class to be created described as JSON formatted string.</param>
    /// <param name="OnReadError">The action to call on an error.</param>
    public static void ReadJsonFromString<T>(T settingsClass, string json, Action OnReadError = null) {
        // Parse json to *this* ScriptableObject
        try {
            JsonUtility.FromJsonOverwrite(json, settingsClass);
        } catch (Exception) {
            // No big deal since we'll initialize all values during OnEnable()
            Debug.Log("Error parsing Yarn Spinner preferences from JSON.");
            OnReadError?.Invoke();
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
