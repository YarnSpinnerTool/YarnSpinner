using UnityEngine;
using System.Collections;

public class MobileOnly : MonoBehaviour {



	// Use this for initialization
	void Awake () {

		if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer ) {
			// Hang around
		} else {
			Destroy(gameObject);
		}
	
	}

}
