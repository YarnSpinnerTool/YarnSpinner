using UnityEngine;
using System.Collections;

/// Removes an object from the scene when the game is running on a non-mobile platform.
/*** used to disable the mobile controls when running outside of a phone or WebGL platform.
*/
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
