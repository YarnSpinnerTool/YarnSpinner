using System.Collections.Generic;
using UnityEngine;

namespace Yarn.Unity {
    /// <summary>
    /// Shows Yarn lines on Canvas Text components.
    /// </summary>
    public class YarnLinesAsCanvasText : MonoBehaviour {
        public YarnProgram yarnScript;
        public UnityEngine.UI.Text[] textCanvases;

        private Dictionary<string, string> _yarnStringTable = new Dictionary<string, string>();

        private void Awake() {
            foreach (var line in yarnScript.GetStringTable()) {
                _yarnStringTable.Add(line.Key, line.Value);
            }
        }

        void Start() {
            var index = 0;
            foreach (var line in _yarnStringTable) {
                if (index >= textCanvases.Length) {
                    return;
                }

                if (textCanvases[index]) {
                    textCanvases[index].text = line.Value;
                }

                index++;
            }
        }
    }
}
