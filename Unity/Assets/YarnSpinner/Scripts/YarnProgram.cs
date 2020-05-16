using CsvHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Yarn;

/// Stores compiled Yarn programs in a form that Unity can serialise.
public class YarnProgram : ScriptableObject
{
    [SerializeField]
    [HideInInspector]
    public byte[] compiledProgram;

    [SerializeField]
    public TextAsset baseLocalisationStringTable;

    [SerializeField]
    public string baseLocalizationId;

    /// <summary>
    /// Available localizations of this yarn program
    /// </summary>
    [SerializeField][HideInInspector]
    public YarnTranslation[] localizations = new YarnTranslation[0];

    /// <summary>
    /// Available voiceovers of this yarn program
    /// </summary>
    [SerializeField][HideInInspector]
    public LinetagToLanguage[] voiceOvers = new LinetagToLanguage[0];

    // Deserializes a compiled Yarn program from the stored bytes in this
    // object.
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
    /// Returns all associated AudioClips for all available Line IDs for the given language ID.
    /// Returns the AudioClips of the language set in preferences if no Parameter is given.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, AudioClip> GetVoiceOversOfLanguage() {
        return GetVoiceOversOfLanguage(Preferences.AudioLanguage);
    }

    /// <summary>
    /// Returns all associated AudioClips for all available Line IDs for the given language ID.
    /// Returns the AudioClips of the language set in preferences if no Parameter is given.
    /// </summary>
    /// <param name="languageId">The ID of the language the requested voice overs.</param>
    /// <returns></returns>
    public Dictionary<string, AudioClip> GetVoiceOversOfLanguage(string languageId) {
        return GetVoiceOversOfLanguage(languageId, voiceOvers);
    }

    /// <summary>
    /// Returns all associated AudioClips for all available Line IDs for the given language ID.
    /// Returns the AudioClips of the language set in preferences if no Parameter is given.
    /// </summary>
    /// <param name="languageId">The ID of the language the requested voice overs.</param>
    /// <param name="voiceOvers">The voice overs array to get a specific language of voice overs from.</param>
    /// <returns></returns>
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
