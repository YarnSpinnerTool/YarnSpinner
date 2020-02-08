using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuOptions : MonoBehaviour {
    public UnityEngine.UI.Dropdown textLanguagesDropdown;
    public UnityEngine.UI.Dropdown audioLanguagesDropdown;

    int textLanguageSelected = -1;
    int audioLanguageSelected = -1;

    private void Awake() {
        if (textLanguagesDropdown) {
            var textLanguageList = new List<string>();
            textLanguageList = Cultures.LanguageNamesToNativeNames(ProjectSettings.TextProjectLanguages.ToArray()).ToList();
            // If no project settings have been defined, show all available cultures
            if (textLanguageList.Count == 0) {
                textLanguageList = Cultures.AvailableCulturesNativeNames.ToList();
            }
            textLanguagesDropdown.AddOptions(textLanguageList);

            textLanguageSelected = textLanguageList.IndexOf(Cultures.LanguageNamesToNativeNames(Preferences.TextLanguage));
#if UNITY_2019_1_OR_NEWER
            textLanguagesDropdown.SetValueWithoutNotify(textLanguageSelected);
#endif
#if UNITY_2018
            textLanguagesDropdown.value = textLanguageSelected;
#endif
        }

        if (audioLanguagesDropdown) {
            var audioLanguagesList = new List<string>();
            audioLanguagesList = Cultures.LanguageNamesToNativeNames(ProjectSettings.AudioProjectLanguages.ToArray()).ToList();

            // If no project settings have been defined, show all available cultures
            if (audioLanguagesList.Count == 0) {
                if (ProjectSettings.TextProjectLanguages.Count == 0) {
                    audioLanguagesList = Cultures.AvailableCulturesNativeNames.ToList();
                } else {
                    audioLanguagesList.Add("No audio languages available!");
                }
            }
            audioLanguagesDropdown.AddOptions(audioLanguagesList);

            audioLanguageSelected = audioLanguagesList.IndexOf(Cultures.LanguageNamesToNativeNames(Preferences.AudioLanguage));
#if UNITY_2019_1_OR_NEWER
            audioLanguagesDropdown.SetValueWithoutNotify(audioLanguageSelected);
#endif
#if UNITY_2018
            audioLanguagesDropdown.value = audioLanguageSelected;
#endif
        }
    }

    public void OnValueChangedTextLanguage(int value) {
        if (textLanguagesDropdown) {
            textLanguageSelected = value;
        }
    }

    public void OnValueChangedAudioLanguage(int value) {
        if (audioLanguagesDropdown) {
            audioLanguageSelected = value;
        }
    }

    public void ApplyPreferences() {
        Preferences.TextLanguage = Cultures.AvailableCultures.First(element => element.NativeName == textLanguagesDropdown.options[textLanguageSelected].text).Name;
        Preferences.AudioLanguage = Cultures.AvailableCultures.First(element => element.NativeName == audioLanguagesDropdown.options[audioLanguageSelected].text).Name;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
