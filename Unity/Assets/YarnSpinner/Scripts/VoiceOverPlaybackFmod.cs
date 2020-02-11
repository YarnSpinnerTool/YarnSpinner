//--------------------------------------------------------------------
//
// This is a Unity behaviour script that demonstrates how to use
// Programmer Sounds and the Audio Table in your game code for 
// the use with the yarn dialogue system. It has been slightly
// altered from this place originally: 
// https://fmod.com/resources/documentation-unity?version=2.0&page=examples-programmer-sounds.html
//
// Programmer sounds allows the game code to receive a callback at a
// sound-designer specified time and return a sound object to the be
// played within the event.
//
// The audio table is a group of audio files compressed in a Bank that
// are not associated with any event and can be accessed by a string key.
//
// Together these two features allow for an efficient implementation of
// dialogue systems where the sound designer can build a single template 
// event and different dialogue sounds can be played through it at runtime.
//
// This script will play one of three pieces of dialog through an event
// on a key press from the player.
//
// This document assumes familiarity with Unity scripting. See
// https://unity3d.com/learn/tutorials/topics/scripting for resources
// on learning Unity scripting. 
//
//--------------------------------------------------------------------
#if FMOD_2_OR_NEWER
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Yarn.Unity {
    class VoiceOverPlaybackFmod : VoiceOverPlaybackBase {
        FMOD.Studio.EVENT_CALLBACK dialogueCallback;

        /// <summary>
        /// The event name of the programmer's sound from the fmod studio project that we trigger voice dialogues from
        /// </summary>
        [FMODUnity.EventRef]
        public string fmodEvent = "";

        /// <summary>
        /// Stores the Dialogue UI instance showing the YarnLine for every voice over instance created. 
        /// Necessary since the sound length is retrieved in a static callback from FMOD.
        /// We call this if we found out the length of the current voice over clip and want the Dialogue UI to wait for 
        /// that length.
        /// </summary>
        private DialogueUIBehaviour _lastVoiceOverDialogueUI;

        /// <summary>
        /// FMOD callbacks are received via a static method. To support multiple instances of this playback class, 
        /// we track which instance fired which fmod audio event in this dict.
        /// </summary>
        private FMOD.Studio.EventInstance _lastVoiceOverEvent;

        private static List<VoiceOverPlaybackFmod> _instances = new List<VoiceOverPlaybackFmod>();

        private void Awake() {
            _instances.Add(this);
        }

        void Start() {
            // Explicitly create the delegate object and assign it to a member so it doesn't get freed
            // by the garbage collected while it's being used
            dialogueCallback = new FMOD.Studio.EVENT_CALLBACK(DialogueEventCallback);
        }

        /// <summary>
        /// Start playback of voice over.
        /// </summary>
        /// <param name="currentLine">The Yarn line currently active.</param>
        /// <param name="voiceOver">The AudioClip accociated with the current Yarn line.</param>
        /// <param name="dialogueUI">The reference to the DialogueUIBehaviour handling this line. Call VoiceOverDuration on this behaviour if you want the UI to wait for audio playback to finish.</param>
        public override void StartLineVoiceOver(Line currentLine, AudioClip voiceOver, DialogueUIBehaviour dialogueUI) {
            // Check if this instance is currently playing back another voice over in which case we stop it
            if (_lastVoiceOverEvent.isValid()) {
                _lastVoiceOverEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }

            // Create playback event
            FMOD.Studio.EventInstance dialogueInstance;
            try {
                dialogueInstance = FMODUnity.RuntimeManager.CreateInstance(fmodEvent);
            } catch (Exception) {
                Debug.LogWarning("FMOD: Voice over playback failed.", gameObject);
                throw;
            }

            _lastVoiceOverDialogueUI = dialogueUI;
            _lastVoiceOverEvent = dialogueInstance;

            // Pin the key string in memory and pass a pointer through the user data
            GCHandle stringHandle = GCHandle.Alloc(currentLine.ID.Remove(0, 5), GCHandleType.Pinned);
            dialogueInstance.setUserData(GCHandle.ToIntPtr(stringHandle));

            dialogueInstance.setCallback(dialogueCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.ALL);
            dialogueInstance.start();
            dialogueInstance.release();
        }

        // TODO: There's currently no way for other parts of the system to tell
        // this object that audio has been interrupted (e.g due to the user
        // requesting to go to the next line in the middle of audio playback.)

        [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
        static FMOD.RESULT DialogueEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, FMOD.Studio.EventInstance instance, IntPtr parameterPtr) {
            // Retrieve the user data
            IntPtr stringPtr;
            instance.getUserData(out stringPtr);

            // Get the string object
            GCHandle stringHandle = GCHandle.FromIntPtr(stringPtr);
            String key = stringHandle.Target as String;

            switch (type) {
                case FMOD.Studio.EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND: {
                        FMOD.MODE soundMode = FMOD.MODE.DEFAULT | FMOD.MODE.CREATESTREAM;
                        var parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));

                        if (key.Contains(".")) {
                            FMOD.Sound dialogueSound;
                            var soundResult = FMODUnity.RuntimeManager.CoreSystem.createSound(Application.streamingAssetsPath + "/" + key, soundMode, out dialogueSound);
                            if (soundResult == FMOD.RESULT.OK) {
                                CallVoiceOverDuration(dialogueSound, instance);
                                parameter.sound = dialogueSound.handle;
                                parameter.subsoundIndex = -1;
                                Marshal.StructureToPtr(parameter, parameterPtr, false);
                            }
                        } else {
                            FMOD.Studio.SOUND_INFO dialogueSoundInfo;
                            var keyResult = FMODUnity.RuntimeManager.StudioSystem.getSoundInfo(key, out dialogueSoundInfo);
                            if (keyResult != FMOD.RESULT.OK) {
                                break;
                            }
                            FMOD.Sound dialogueSound;
                            var soundResult = FMODUnity.RuntimeManager.CoreSystem.createSound(dialogueSoundInfo.name_or_data, soundMode | dialogueSoundInfo.mode, ref dialogueSoundInfo.exinfo, out dialogueSound);
                            if (soundResult == FMOD.RESULT.OK) {
                                CallVoiceOverDuration(dialogueSound, instance);
                                parameter.sound = dialogueSound.handle;
                                parameter.subsoundIndex = dialogueSoundInfo.subsoundindex;
                                Marshal.StructureToPtr(parameter, parameterPtr, false);
                            }
                        }
                    }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND: {
                        var parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));
                        var sound = new FMOD.Sound();
                        sound.handle = parameter.sound;
                        sound.release();
                    }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROYED:
                    foreach (var playbackInstance in _instances) {
                        if (playbackInstance._lastVoiceOverEvent.Equals(instance)) {
                            playbackInstance._lastVoiceOverDialogueUI = null;
                        }
                    }

                    // Now the event has been destroyed, unpin the string memory so it can be garbage collected
                    stringHandle.Free();
                    break;
            }
            return FMOD.RESULT.OK;
        }

        /// <summary>
        /// Call the _voiceOverDuration action for this sound.
        /// </summary>
        /// <param name="dialogueSound">The sound for which we want to call _voiceOverDuration.</param>
        private static void CallVoiceOverDuration(FMOD.Sound dialogueSound, FMOD.Studio.EventInstance instance) {
            var soundLength = GetSoundLength(dialogueSound);
            // Only tell the Dialogue UI to wait if we actually got a sound length
            if (soundLength >= 0) {
                foreach (var playbackInstance in _instances) {
                    if (playbackInstance._lastVoiceOverEvent.Equals(instance)) {
                        playbackInstance._lastVoiceOverDialogueUI?.VoiceOverDuration(soundLength);
                    } else {
                        Debug.Log("FMOD: Current playback event instance unknown. Will not wait for this line to finish.");
                    }
                }
            }
        }

        /// <summary>
        /// Returns the length of a sound.
        /// CAREFUL: you'll need to create the sound with 
        /// FMOD.MODE = FMOD.MODE.DEFAULT | FMOD.MODE.CREATESTREAM;
        /// Other modes could work but a lot will result in "ERR_NOTREADY".
        /// </summary>
        /// <param name="dialogueSound">The sound we want to get the length of.</param>
        /// <returns></returns>
        private static float GetSoundLength(FMOD.Sound dialogueSound) {
            uint soundLength;
            var lengthResult = dialogueSound.getLength(out soundLength, FMOD.TIMEUNIT.MS);
            var soundLengthInSeconds = (float)soundLength / 1000f;
            if (lengthResult == FMOD.RESULT.OK) {
                //Debug.Log("Sound length is: " + soundLengthInSeconds + "s.");
                return soundLengthInSeconds;
            } else {
                Debug.LogWarning(lengthResult.ToString());
                return -1;
            }
        }
    }
}
#endif
