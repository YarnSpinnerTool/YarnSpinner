using UnityEngine;
using System.Collections;
using System.Collections.Generic;


namespace Yarn.Unity.Example {
	public class Character : MonoBehaviour {

		public float minPosition = -5.3f;
		public float maxPosition = 5.3f;

		public float moveSpeed = 1.0f;

		public float interactionRadius = 2.0f;

		// Draw the range at which we'll start talking to people.
		void OnDrawGizmosSelected() {
			Gizmos.color = Color.blue;

			// Flatten the sphere into a disk, which looks nicer in 2D games
			Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(1,1,0));

			// Need to draw at position zero because we set position in the line above
			Gizmos.DrawWireSphere(Vector3.zero, interactionRadius);
		}

		
		// Update is called once per frame
		void Update () {

			// Remove all player control when we're in dialogue
			if (FindObjectOfType<DialogueRunner>().isDialogueRunning == true) {
				return;
			}

			var movement = Input.GetAxis("Horizontal") * moveSpeed *Time.deltaTime;

			var newPosition = transform.position;
			newPosition.x += movement;
			newPosition.x = Mathf.Clamp(newPosition.x, minPosition, maxPosition);

			transform.position = newPosition;

			if (Input.GetKeyDown(KeyCode.Space)) {
				// Find all DialogueParticipants, and filter them to
				// those that have a script and in range; then start a conversation
				// with the first one

				var allParticipants = 
					new List<DialogueParticipant>(FindObjectsOfType<DialogueParticipant>());

				var target = allParticipants.Find(delegate(DialogueParticipant p) {
					return p.script != null &&
						(p.transform.position - this.transform.position)
							.magnitude <= interactionRadius;
				});

				FindObjectOfType<DialogueRunner>().StartDialogue(target.startNode);

			}
		}
	}

}
