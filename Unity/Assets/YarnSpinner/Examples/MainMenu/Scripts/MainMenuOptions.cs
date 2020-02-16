using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class MainMenuOptions : MonoBehaviour {
    public Dropdown textLanguagesDropdown;
    public Dropdown audioLanguagesDropdown;
    public TMP_Dropdown textLanguagesTMPDropdown;
    public TMP_Dropdown audioLanguagesTMPDropdown;

    [SerializeField] Yarn.Unity.YarnLinesAsCanvasText[] _yarnLinesCanvasTexts = default;

    int textLanguageSelected = -1;
    int audioLanguageSelected = -1;

    private void Awake() {
        LoadTextLanguagesIntoDropdowns();
        LoadAudioLanguagesIntoDropdowns();
    }

    public void OnValueChangedTextLanguage(int value) {
        ApplyChangedValueToPreferences(value, ref textLanguageSelected, textLanguagesTMPDropdown, textLanguagesDropdown);

        foreach (var yarnLinesCanvasText in _yarnLinesCanvasTexts) {
            yarnLinesCanvasText?.OnTextLanguagePreferenceChanged();
        }
    }

    public void OnValueChangedAudioLanguage(int value) {
        ApplyChangedValueToPreferences(value, ref audioLanguageSelected, audioLanguagesTMPDropdown, audioLanguagesDropdown);
    }

    public void ReloadScene() {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void LoadTextLanguagesIntoDropdowns() {
        if (textLanguagesDropdown || textLanguagesTMPDropdown) {
            var textLanguageList = new List<string>();
            textLanguageList = Cultures.LanguageNamesToNativeNames(ProjectSettings.TextProjectLanguages.ToArray()).ToList();
            // If no project settings have been defined, show all available cultures
            if (textLanguageList.Count == 0) {
                textLanguageList = Cultures.AvailableCulturesNativeNames.ToList();
            }

            PopulateLanguagesListToDropdown(textLanguageList, textLanguagesTMPDropdown, textLanguagesDropdown, ref textLanguageSelected);
        }
    }

    private void LoadAudioLanguagesIntoDropdowns() {
        if (audioLanguagesDropdown || audioLanguagesTMPDropdown) {
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

            PopulateLanguagesListToDropdown(audioLanguagesList, audioLanguagesTMPDropdown, audioLanguagesDropdown, ref audioLanguageSelected);
        }
    }

    private void PopulateLanguagesListToDropdown(List<string> languageList, TMP_Dropdown tmpDropdown, Dropdown dropdown, ref int selectedLanguageIndex) {
        selectedLanguageIndex = languageList.IndexOf(Cultures.LanguageNamesToNativeNames(Preferences.TextLanguage));

        if (dropdown) {
            dropdown.ClearOptions();
            dropdown.AddOptions(languageList);
#if UNITY_2019_1_OR_NEWER
            dropdown.SetValueWithoutNotify(selectedLanguageIndex);
#else
            dropdown.value = selectedLanguageIndex;
#endif
        }

        if (tmpDropdown) {
            tmpDropdown.ClearOptions();
            tmpDropdown.AddOptions(languageList);
#if UNITY_2019_1_OR_NEWER
            tmpDropdown.SetValueWithoutNotify(selectedLanguageIndex);
#else
            tmpDropdown.value = selectedLanguageIndex;
#endif
        }
    }

    private void ApplyChangedValueToPreferences(int value, ref int languageSelected, TMP_Dropdown tmpDropdown, Dropdown dropdown) {
        languageSelected = value;

        if (dropdown) {
            Preferences.TextLanguage = Cultures.AvailableCultures.First(element => element.NativeName == dropdown.options[value].text).Name;
        }
        if (tmpDropdown) {
            Preferences.TextLanguage = Cultures.AvailableCultures.First(element => element.NativeName == tmpDropdown.options[value].text).Name;
        }
    }
}
