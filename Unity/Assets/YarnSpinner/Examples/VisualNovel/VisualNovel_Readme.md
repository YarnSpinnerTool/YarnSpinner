# Visual Novel example documentation

This is an example project that implements a few Yarn Spinner features:

- `DialogueUISplit.cs` shows how to separate the speaker's name from their dialogue, and how to display them separately
- `VNManager.cs` demos extensive use of Yarn Commands to implement custom visual novel functionality (sprite management, sound playback, screen fades)

This is intended as an example of how Yarn Spinner is flexible enough to support a variety of game formats and genres. **It is not actively developed, it is just a template / example.** If you want to add new commands of functionality, try to figure it out yourself!

# Quickstart

- if you're new to Yarn Spinner, read the [tutorial](https://yarnspinner.dev/docs/tutorial).
- open and play the demo scene at `\VisualNovel\Scenes\VNSceneExample.unity`
- read the example Yarn script at `\VisualNovel\Dialogue\VNExampleDialogue.yarn`
- to put it all into a scene, use the prefab at `\VisualNovel\Prefabs\VisualNovelPrefab.prefab`

# Notes on importing character sprites

For reference, the example character sprites are about 600 x 1200 pixels. Because they are instantiated automatically, the default sprite settings matter a lot. Some suggested importer settings:

- Texture Type: Sprite
- Pixels Per Unit: 165 (try different numbers until the size feels right)
- Pivot: Bottom

# VN Commands API reference

If you're new to Yarn Spinner, read about [Yarn Commands](https://yarnspinner.dev/docs/unity/adding-command-handlers/).

API example: for the command `<<Draw (spriteName), (x=0.5), (y=0.5)>>`, the `"=0.5"` is a default value used if you don't specify the number yourself. So you can type `<<Draw house01>>` or `<<Draw DogHappy, 0.75>>` and it's ok, it will fill in the other parameters with default values.


# BASIC COMMANDS

### `<<Scene (spriteName)>>`
set background image to (spriteName)

---

### `<<Draw (spriteName), (x=0.5), (y=0.5)>>`
show (spriteName) at (x,y) in normalized screenspace (0.0-1.0)
- 0.0 is like 0% left / bottom of screen, 1.0 is like 100% right / top of screen
- for x or y, you can also use keywords like "right", "upper", "top" (0.75) or "middle", "center" (0.5) or "left", "lower", "bottom" (0.25)
- when x and/or y are omitted (e.g. `<<Show TreeSprite>>`) then they default to values of 0.5... thus, omitting both x and y would set the position to (0.5, 0.5) in normalized screenspace (center of the screen)

---

### `<<Hide (actorOrSpriteName)>>`
hide and delete any actor called (actorOrSpriteName) or any sprites called (actorOrSpriteName)
- this command supports wildcards (*)
    - example 1: `<<Hide Sally*>>` will hide all actors and sprites with names that start with "Sally"
    - example 2: `<<Hide *Cat>>` will hide all objects with names that end with "Cat"

---

### `<<HideAll>>`
hides all sprites and actors, takes no parameters

---

### `<<Act (actorName), (spriteName), (x=0.5), (y=0.5), (HTMLcolor=black) >>`
very useful for persistent characters... kind of like `<<Show>>` except it also has (actorName), which links the sprite to that actorName
- anytime a character named (actorName) talks in a Yarn script, this sprite will automatically highlight
- also good for changing expressions, e.g. calling `<<Act Dog, dog_happy, left>>` and then `<<Act Dog, dog_sad>>` will automatically swap the sprite and preserve its positioning... this is better than tediously calling `<<Show dog_happy>>` and then `<<Hide dog_happy>>` and then `<<Show dog_sad>>`, etc.
- (HTMLcolor) is a web hex color (like "#ffffff") or a common color keyword (like "white")... here, it affects the color of the name label in VNDialogueUI, but I suppose you could use it for anything

---


# SPRITE TRANSFORM COMMANDS

### `<<Flip (actorOrSpriteName), (xPosition=0.0)>>`
horizontally flips an actor or sprite, in case you need to make your characters look around or whatever
- if (xPosition) is defined, it will force the actor to face a specific direction

---

### `<<Move (actorOrSpriteName), (xPosition=0.5), (yPosition=0.5), (moveTime=1.0)>>`
animates / slides an actor or sprite toward a new position, good for animating walking or running
- (moveTime) is how long it will take to move from current position to new position, in seconds
- for notes on (xPosition) and (yPosition), see entry for `<<Show>>` above
- KNOWN BUG: not recommended to use this at the same time as a `<<Shake>>` call... let the previous call finish, first

---

### `<<Shake (actorOrSpriteName), (shakeTime=0.5)>>`
shakes an actor or a sprite, good for when someone is surprised or angry or laughing
- if shakeTime isn't specified, the default value 0.5 means "shake this object at 50% strength for 0.5 seconds"
- a shakeTime=1.0 means "shake this object at 100% for 1.0 seconds", and so on
- KNOWN BUG: not recommended to use this at the same time as a `<<Move>>` call... let the previous call finish, first

---


# CAMERA COMMANDS

### `<<Fade (HTMLcolor=black), (startOpacity=0.0), (endOpacity=1.0), (fadeTime=1.0)>>`
fades the screen to a certain color, could be a fade in or a fade out depending on how you use it
- startOpacity and endOpacity are numbers 0.0-1.0 (like 0%-100%)
- by default with no parameters (`<<Fade>>`) will fade to black over 1.0 seconds
- (HTMLcolor) is a web hex color (like "#ffffff") or a common color keyword (like "white")

---

### `<<FadeIn (fadeTime=1.0)>>`
useful shortcut for fading in, no matter what the previous fade settings were... equivalent to calling something like `<<Fade black, -1, 0.0, 1.0>>` (where startOpacity=-1 means it will reuse the last fade command's colors and opacity)

---

### `<<CamOffset (xOffset=0), (yOffset=0), (moveTime=0.25)>>`
pans the camera based on x/y offset from the center over "moveTime" seconds
- example: ``<<CamOffset 0, 0, 0.1>>` centers the camera (default position) over 0.1 seconds
- note that the camera doesn't actually move, but fakes a camera movement by moving sprites
- by default, the background image is fixed like a skybox; if you want the background to pan too, then parent it to the "Sprites" game object in the prefab

---


# AUDIO COMMANDS

### `<<PlayAudio (audioClipName),(volume),("loop")>>`
plays an audio clip named (audioClipName) at volume (0.0-1.0)
- if "loop" is present, it will loop the audio repeatedly until stopped; otherwise, the sound will end automatically

---

### `<<StopAudio (audioClipName)>>`
stop playing any active audio clip named (audioClipName)

---

### `<<StopAudioAll>>`
stops playing all sounds, no parameters

---

# ASSET CREDITS
The assets included in this example project are:

- Visual Novel Tutorial Set (public domain) https://opengameart.org/content/visual-novel-tutorial-set
- Lovely Piano Song by Rafael Krux (public domain) https://freepd.com
- Comic Game Loop - Mischief by Kevin MacLeod (public domain) https://freepd.com