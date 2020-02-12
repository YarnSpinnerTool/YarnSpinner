using System.Collections.Generic;
using UnityEngine;

namespace Yarn.Unity {
    public class YarnLinesAsCanvasText : MonoBehaviour {
        public YarnProgram yarnScript;
        private Dictionary<string, string> strings = new Dictionary<string, string>();
        public UnityEngine.UI.Text[] textObjects;

        private void Awake() {
            foreach (var line in yarnScript.GetStringTable()) {
                strings.Add(line.Key, line.Value);
            }
        }

        // Start is called before the first frame update
        void Start() {
            var index = 0;
            foreach (var line in strings) {
                if (index >= textObjects.Length) {
                    return;
                }

                if (textObjects[index]) {
                    textObjects[index].text = line.Value;
                }

                index++;
            }
        }
    }
}
