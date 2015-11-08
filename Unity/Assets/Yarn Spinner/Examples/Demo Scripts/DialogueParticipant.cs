using UnityEngine;
using System.Collections;
using UnityEngine.Serialization;

namespace Yarn.Unity.Example {
	public class DialogueParticipant : MonoBehaviour {
		
		public string characterName = "";

		public TextAsset scriptToLoad;
		public string startNode = "";

		
		// Use this for initialization
		void Start () {
			if (scriptToLoad != null) {
				FindObjectOfType<Yarn.Unity.DialogueRunner>().AddScript(scriptToLoad);
			}
			
		}
		
		// Update is called once per frame
		void Update () {
			
		}
	}

}
