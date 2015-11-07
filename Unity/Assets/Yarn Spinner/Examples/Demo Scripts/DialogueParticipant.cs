using UnityEngine;
using System.Collections;

namespace Yarn.Unity.Example {
	public class DialogueParticipant : MonoBehaviour {
		
		public string characterName = "";
		public TextAsset script;
		public string startNode = "";

		
		// Use this for initialization
		void Start () {
			if (script != null) {
				FindObjectOfType<Yarn.Unity.DialogueRunner>().AddScript(script);
			}
			
		}
		
		// Update is called once per frame
		void Update () {
			
		}
	}

}
