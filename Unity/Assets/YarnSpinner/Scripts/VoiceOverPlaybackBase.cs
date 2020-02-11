using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yarn.Unity {
    public abstract class VoiceOverPlaybackBase : MonoBehaviour {
        public abstract void StartLineVoiceOver(Yarn.Line currentLine, AudioClip voiceOver, DialogueUIBehaviour dialogueUI);
    }
}
