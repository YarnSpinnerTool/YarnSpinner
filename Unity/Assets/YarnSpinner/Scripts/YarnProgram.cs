using CsvHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Yarn;

/// <summary>
/// A <see cref="ScriptableObject"/> created from a yarn file that stores the compiled Yarn program, all lines of text with their associated IDs, translations and voice over <see cref="AudioClip"/>s.
/// </summary>
public class YarnProgram : ScriptableObject
{
    /// <summary>
    /// The compiled Yarn program as byte code.
    /// </summary>
    [SerializeField]
    [HideInInspector]
    public byte[] compiledProgram;

    [SerializeField]
    public TextAsset baseLocalisationStringTable;

    /// <summary>
    /// The language ID (e.g. "en" or "de") of the base language (the language the Yarn file is written in).
    /// </summary>
    [SerializeField]
    public string baseLocalizationId;

    /// <summary>
    /// Available localizations of this <see cref="YarnProgram"/>.
    /// </summary>
    [SerializeField][HideInInspector]
    public YarnTranslation[] localizations = new YarnTranslation[0];

    /// <summary>
    /// Available voice over audio clips of this <see cref="YarnProgram"/>.
    /// </summary>
    [SerializeField][HideInInspector]
    public LinetagToLanguage[] voiceOvers = new LinetagToLanguage[0];

    /// <summary>
    /// Deserializes a compiled Yarn program from the stored bytes in this object.
    /// </summary>
    /// <returns></returns>
    public Program GetProgram() {
        return Program.Parser.ParseFrom(compiledProgram);                
    }


    /// <summary>
    /// Returns a tagged string table from all lines available on this yarn asset in the language given in preferences.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, string> GetStringTable() {
        var textToLoad = new TextAsset();
        if (localizations != null || localizations.Length > 0) {
            textToLoad = Array.Find(localizations, element => element.languageName == Preferences.TextLanguage)?.text;
        }
        if (textToLoad == null || string.IsNullOrEmpty(textToLoad.text)) {
            textToLoad = baseLocalisationStringTable;
        }

        return GetStringTable(textToLoad);
    }

    /// <summary>
    /// Returns a tagged string table from all lines available on this yarn asset in the given language.
    /// </summary>
    /// <param name="languageId">The language id of the returned string table.</param>
    /// <returns></returns>
    public Dictionary<string, string> GetStringTable (string languageId) {
        if (languageId == baseLocalizationId) {
            return GetStringTable(baseLocalisationStringTable);
        } else if (localizations.FirstOrDefault(element => element.languageName == languageId) != null) {
            return GetStringTable(localizations.FirstOrDefault(element => element.languageName == languageId).text);
        } else {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Returns a tagged string table from all lines available on this yarn asset.
    /// </summary>
    /// <param name="stringTable">The (localized) string table of this yarn asset to load.</param>
    /// <returns></returns>
    public static Dictionary<string, string> GetStringTable (TextAsset stringTable) {
        Dictionary<string, string> strings = new Dictionary<string, string>();
        if (stringTable == null) {
            return strings;
        }

        // Use the invariant culture when parsing the CSV to ensure parsing
        // with the correct separator instead of the user's locale separator
        var configuration = new CsvHelper.Configuration.Configuration(
            System.Globalization.CultureInfo.InvariantCulture
        );

        using (var reader = new System.IO.StringReader(stringTable.text))
        using (var csv = new CsvReader(reader, configuration)) {
            csv.Read();
            csv.ReadHeader();

            while (csv.Read()) {
                strings.Add(csv.GetField("id"), csv.GetField("text"));
            }
        }

        return strings;
    }


    /// <summary>
    /// Returns all associated <see cref="AudioClip"/>s for all available Line IDs of this
    /// <see cref="YarnProgram"/> for the language set in <see cref="Preferences"/>.
    /// </summary>
    /// <returns>A collection of all <see cref="AudioClip"/>s (value) associated with their 
    /// linetag/StringID (key) that are available on this <see cref="YarnProgram"/>.</returns>
    public Dictionary<string, AudioClip> GetVoiceOversOfLanguage() {
        return GetVoiceOversOfLanguage(Preferences.AudioLanguage);
    }

    /// <summary>
    /// Returns all associated <see cref="AudioClip"/>s for all available Line IDs for the given
    /// language ID. Returns the <see cref="AudioClip"/>s of the language set in <see 
    /// cref="Preferences"/> if no parameter is given.
    /// </summary>
    /// <param name="languageId">The ID of the language the requested voice overs.</param>
    /// <returns>A collection of all <see cref="AudioClip"/>s (value) associated with their
    /// linetag/StringID (key) that are available on this <see cref="YarnProgram"/>.</returns>
    public Dictionary<string, AudioClip> GetVoiceOversOfLanguage(string languageId) {
        return GetVoiceOversOfLanguage(languageId, voiceOvers);
    }

    /// <summary>
    /// Returns all associated <see cref="AudioClip"/>s for all available Line IDs for the given
    /// language ID. Returns the <see cref="AudioClip"/>s of the language set in <see 
    /// cref="Preferences"/> if no parameter is given.
    /// </summary>
    /// <param name="languageId">The ID of the language the requested voice overs.</param>
    /// <param name="voiceOvers">The voice overs array to get a specific language of voice overs from.</param>
    /// <returns>A collection of all <see cref="AudioClip"/>s (value) associated with their
    /// linetag/StringID (key) that are available on this <see cref="YarnProgram"/>.</returns>
    public static Dictionary<string, AudioClip> GetVoiceOversOfLanguage(string languageId, LinetagToLanguage[] voiceOvers) {
        Dictionary<string, AudioClip> voiceOversOfPreferredLanguage = new Dictionary<string, AudioClip>();
        if (string.IsNullOrEmpty(languageId)) {
            return voiceOversOfPreferredLanguage;
        }

        foreach (var line in voiceOvers) {
            foreach (var language in line.languageToAudioclip) {
                if (language.language == languageId) {
                    voiceOversOfPreferredLanguage.Add(line.linetag, language.audioClip);
                }
            }
        }

        return voiceOversOfPreferredLanguage;
    }

#if ADDRESSABLES
    /// <summary>
    /// Asynchronously loads all associated <see cref="AudioClip"/>s for all available Line IDs of this <see cref="YarnProgram"/>
    /// for the language set in <see cref="Preferences"/>. Will return every single voice over once it completed loading via the
    /// given callback action provided as parameter.
    /// </summary>
    /// <param name="action">The action to call after the loading of each voice over <see cref="AudioClip"/> completed. The action should create a collection associating the linetags/StringIDs with the corresponding voice over.</param>
    /// <returns>A collection of <see cref="Task"/>s loading all voice over <see cref="AudioClip"/>s available on 
    /// this <see cref="YarnProgram"/>.</returns>
    public async Task<IEnumerable<AudioClip>> GetVoiceOversOfLanguageAsync(Action<string, AudioClip> action) {
        List<Task<AudioClip>> listOfTasks = new List<Task<AudioClip>>();
        foreach (var linetag in voiceOvers) {
            foreach (var language in linetag.languageToAudioclip) {
                // Only load the preferred audio language
                if (language.language != Preferences.AudioLanguage) {
                    continue;
                }
                // check Addressable for NULL
                if (!language.audioClipAddressable.RuntimeKeyIsValid()) {
                    continue;
                }
                var task = language.audioClipAddressable.LoadAssetAsync<AudioClip>();
                task.Completed += (AsyncOperationHandle<AudioClip> asyncOperationHandle) => {
                    if (!asyncOperationHandle.Result) {
                        Debug.LogWarning("Got NULL for Addressable: " + linetag.linetag);
                        return;
                    } else {
                        action(linetag.linetag, asyncOperationHandle.Result);
                        Debug.Log("Found something!");
                    }
                };
                listOfTasks.Add(task.Task);
            }
        }
        return await Task.WhenAll(listOfTasks);
    }
#endif
}
