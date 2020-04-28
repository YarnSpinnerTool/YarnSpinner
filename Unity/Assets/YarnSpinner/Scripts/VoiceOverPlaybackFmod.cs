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
using System.Collections;

namespace Yarn.Unity {
    class VoiceOverPlaybackFmod : DialogueViewBase {
        FMOD.Studio.EVENT_CALLBACK dialogueCallback;

        /// <summary>
        /// The event name of the programmer's sound from the fmod studio project that we trigger voice dialogues from
        /// </summary>
        [FMODUnity.EventRef]
        public string fmodEvent = "";

        /// <summary>
        /// When true, the Runner has signaled to finish the current line 
        /// asap.
        /// </summary>
        private bool finishCurrentLine = false;

        /// <summary>
        /// FMOD callbacks are received via a static method. To support multiple instances of this playback class,
        /// we track the last fired FMOD event per instance here.
        /// </summary>
        private FMOD.Studio.EventInstance _lastVoiceOverEvent;

        /// <summary>
        /// All instances currently alive of this class. Necessary to properly deal with static callbacks from FMOD.
        /// </summary>
        private static List<VoiceOverPlaybackFmod> _instances = new List<VoiceOverPlaybackFmod>();

        private void OnEnable() {
            _instances.Add(this);
        }

        private void OnDisable() {
            _instances.Remove(this);
        }

        void Start() {
            // Explicitly create the delegate object and assign it to a member so it doesn't get freed
            // by the garbage collected while it's being used
            dialogueCallback = new FMOD.Studio.EVENT_CALLBACK(DialogueEventCallback);
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
                    // Now the event has been destroyed, unpin the string memory so it can be garbage collected
                    stringHandle.Free();
                    break;
            }
            return FMOD.RESULT.OK;
        }

        protected override IEnumerator RunLine(LocalizedLine dialogueLine) {
            finishCurrentLine = false;

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

            _lastVoiceOverEvent = dialogueInstance;

            // Pin the key string in memory and pass a pointer through the user data
            GCHandle stringHandle = GCHandle.Alloc(dialogueLine.TextID.Remove(0, 5), GCHandleType.Pinned);
            dialogueInstance.setUserData(GCHandle.ToIntPtr(stringHandle));

            dialogueInstance.setCallback(dialogueCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.ALL);
            dialogueInstance.start();
            dialogueInstance.release();

            while (!finishCurrentLine && dialogueInstance.isValid()) {
                yield return null;
            }

            if (dialogueInstance.isValid()) {
                dialogueInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
        }

        protected override void FinishCurrentLine() {
            finishCurrentLine = true;
        }

        protected override IEnumerator EndCurrentLine() {
            // Avoid skipping lines if textSpeed == 0
            yield return new WaitForEndOfFrame();
        }

        public override void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected) {
            // Do nothing
        }

        public override Dialogue.HandlerExecutionType RunCommand(Command command, Action onCommandComplete) {
            return Dialogue.HandlerExecutionType.ContinueExecution;
        }

        internal override void OnFinishedLineOnAllViews() {
            // Do nothing
        }
    }
}
#endif
