using UnityEngine;
using System.Collections;

namespace Yarn.Unity.Example {
	
	public class CameraFollow : MonoBehaviour {

		public Transform target;

		public float minPosition = -5.3f;
		public float maxPosition = 5.3f;
		
		public float moveSpeed = 1.0f;

		// Update is called once per frame
		void Update () {
			if (target == null) {
				return;
			}
			var newPosition = Vector3.Lerp(transform.position, target.position, moveSpeed * Time.deltaTime);

			newPosition.x = Mathf.Clamp(newPosition.x, minPosition, maxPosition);
			newPosition.y = transform.position.y;
			newPosition.z = transform.position.z;

			transform.position = newPosition;
		}
	}
}

