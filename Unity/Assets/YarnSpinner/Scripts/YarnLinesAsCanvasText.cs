using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Yarn.Unity {
    /// <summary>
    /// Shows Yarn lines on Canvas Text components.
    /// </summary>
    public class YarnLinesAsCanvasText : MonoBehaviour {
        public YarnProgram yarnScript;
        public UnityEngine.UI.Text[] textCanvases;
        public TextMeshProUGUI[] textMeshProCanvases;
        [SerializeField] bool _useTextMeshPro = default;

        private Dictionary<string, string> _yarnStringTable = new Dictionary<string, string>();

        private void Awake() {
            LoadStringTable();
        }

        void Start() {
            UpdateTextOnUiElements();
        }

        /// <summary>
        /// Reload the string table and update the UI elements.
        /// Useful if the languages preferences were changed.
        /// </summary>
        public void OnTextLanguagePreferenceChanged () {
            LoadStringTable();
            UpdateTextOnUiElements();
        }

        /// <summary>
        /// Load all strings from the yarn file into memory.
        /// </summary>
        private void LoadStringTable() {
            if (yarnScript != null) {
                _yarnStringTable.Clear();
                foreach (var line in yarnScript.GetStringTable()) {
                    _yarnStringTable.Add(line.Key, line.Value);
                }
            }
        }

        /// <summary>
        /// Update all UI components to the yarn lines loaded from yarnScript.
        /// </summary>
        private void UpdateTextOnUiElements() {
            var index = 0;
            foreach (var line in _yarnStringTable) {
                if (!_useTextMeshPro && index < textCanvases.Length && textCanvases[index]) {
                    textCanvases[index].text = line.Value;
                }

                if (_useTextMeshPro && index < textMeshProCanvases.Length && textMeshProCanvases[index]) {
                    textMeshProCanvases[index].text = line.Value;
                }

                index++;
            }
        }
    }
}
