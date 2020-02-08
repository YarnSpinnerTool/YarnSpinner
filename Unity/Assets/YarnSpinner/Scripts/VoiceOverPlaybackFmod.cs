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
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

class VoiceOverPlaybackFmod : MonoBehaviour {
    FMOD.Studio.EVENT_CALLBACK dialogueCallback;
    
    /// <summary>
    /// The event name of the programmer's sound from the fmod studio project that we trigger voice dialogues from
    /// </summary>
    [FMODUnity.EventRef]
    public string fmodEvent = "";
    
    /// <summary>
    /// We call this action when we voiceover file started successfully
    /// </summary>
    private static System.Action _onVoiceoverTriggeredSuccessfully;

    /// <summary>
    /// We call this when the voiceover file playback finished
    /// </summary>
    private static System.Action _onVoiceoverFinish;

    void Start() {
        // Explicitly create the delegate object and assign it to a member so it doesn't get freed
        // by the garbage collected while it's being used
        dialogueCallback = new FMOD.Studio.EVENT_CALLBACK(DialogueEventCallback);
    }

    public void PlayDialogue(string key, System.Action onVoiceoverTriggeredSuccessfully, System.Action onVoiceoverFinish) {
        FMOD.Studio.EventInstance dialogueInstance;
        try {
            dialogueInstance = FMODUnity.RuntimeManager.CreateInstance(fmodEvent);
        } catch (Exception) {
            _onVoiceoverFinish?.Invoke();
            throw;
        }

        _onVoiceoverTriggeredSuccessfully = onVoiceoverTriggeredSuccessfully;
        _onVoiceoverFinish = onVoiceoverFinish;

        // Pin the key string in memory and pass a pointer through the user data
        GCHandle stringHandle = GCHandle.Alloc(key, GCHandleType.Pinned);
        dialogueInstance.setUserData(GCHandle.ToIntPtr(stringHandle));

        dialogueInstance.setCallback(dialogueCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.ALL);
        dialogueInstance.start();
        dialogueInstance.release();
    }

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
                    FMOD.MODE soundMode = FMOD.MODE.LOOP_NORMAL | FMOD.MODE.CREATECOMPRESSEDSAMPLE | FMOD.MODE.NONBLOCKING;
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
                            _onVoiceoverTriggeredSuccessfully?.Invoke();
                        }
                    }
                }
                break;
            case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND: {
                    var parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));
                    var sound = new FMOD.Sound();
                    sound.handle = parameter.sound;
                    _onVoiceoverFinish?.Invoke();
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
}
#endif