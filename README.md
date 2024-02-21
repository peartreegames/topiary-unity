# Topiary.Unity

Unity Integration for the narrative scripting tool [topiary](https://github.com/peartreegames/topiary) with the [C# bindings](https://github.com/peartreegames/topiary-csharp).

Checkout the [syntax](https://github.com/peartreegames/topiary/blob/main/docs/syntax.md) file if you're new to writing with Topi.

## Installation

Can be installed via the Package Manager > Add Package From Git URL...

This repo has a dependency on the EvtVariable package which MUST be installed first. (From my understanding Unity does not allow git urls to be used as dependencies of packages) https://github.com/peartreegames/evt-variables.git

This package also depends on the Unity Addressables package, which should automatically be installed, but if you any issues please install it manually from the Package Manager.

then the repo can be added

https://github.com/peartreegames/topiary-unity.git

## Setup

You'll need some sort of singleton DialogueRunner to go between Topiary and your UI.

Here's a rough sketch:

```csharp
public class DialogueRunner : MonoBehaviour
{
    private void Awake()
    {
        Conversation.OnStart += OnStart;
        Conversation.OnEnd += OnEnd;
        Conversation.OnDialogue += OnDialogue;
        Conversation.OnChoices += OnChoices;
    }

    private void OnDestroy()
    {
        Conversation.OnStart -= OnStart;
        Conversation.OnEnd -= OnEnd;
        Conversation.OnDialogue -= OnDialogue;
        Conversation.OnChoices -= OnChoices;
    }

    private void OnStart(Story story, Conversation conversation)
    {
        // Update the UI
        // Perform any tasks for dialogue mode
    }

    private void OnDialogue(Story story, PeartreeGames.Topiary.Dialogue dialogue,
        TopiSpeaker topiSpeaker)
    {
        // Update the UI to display the dialogue
        // Use story.SelectContinue() to continue the story
    }

    private void OnChoices(Story story, Choice[] choices)
    {
        // Update the UI to display the choices
        // Use story.SelectChoice(int) when the player makes their selection
    }


    private void OnEnd(Story story, Conversation conversation)
    {
        // Close your UI
        // Perform any clean up tasks
        // Revert back to game mode
    }
}
```

## Setup

Any `.topi` file will automatically be compiled and a subasset will be created with the `.topib` bytecode.

Once your file is compiled it will automatically be added to a Topiary addressables group with the labels `Topiary` and `Topi`.

Add a `Conversation` MonoBehaviour to a GameObject and select the `.topi` file you want to associate with that Conversation.

Trigger the start of the Conversation in anyway you like with `conversation.PlayStory()` or `StartCoroutine(conversation.Play())`

## Functions

Topiary can call external functions that are marked with the `Topi` attribute. Here's some examples:

```csharp
// Preserve used to make sure Unity doesn't strip these functions from your build
public static class DialogueFunctions
{
    // playAnim will be replaced with the C# method
    // we give the playAnim function a body in our .topi file for testing
    // a warning will be shown if any extern isn't set when we start our Story
    // ex .topi file:
    //      extern const playAnim = |name, clip| {}
    //      playAnim("Player", "Laugh")
    [Topi("playAnim"), Preserve]
    public static void PlayAnim(TopiValue speakerName, TopiValue animClip)
    {
        if (!Conversation.Speakers.TryGetValue(speakerName.String, out var speaker)) return;
        speaker.GetComponent<Speaker>().PlayAnim(animClip.String);
    }

    [Topi("triggerAnim"), Preserve]
    public static void TriggerAnim(TopiValue speakerName, TopiValue animClip)
    {
        if (!Conversation.Speakers.TryGetValue(speakerName.String, out var speaker)) return;
        speaker.GetComponent<Speaker>().TriggerAnim(animClip.String);
    }
    
    [Topi("stopAnim"), Preserve]
    public static void StopAnim(TopiValue speakerName, TopiValue animClip)
    {
        if (!Conversation.Speakers.TryGetValue(speakerName.String, out var speaker)) return;
        speaker.GetComponent<Speaker>().StopAnim(animClip.String);
    }
}
```

## TopiValue

I wanted to hide away the [TopiValue](https://github.com/peartreegames/topiary-csharp/blob/main/Topiary/TopiValue.cs) implementation details, but without boxing everything to just `object` it didn't seem viable.

So instead here's your warning: TopiValues have data are explicitly mapped out in memory and different fields are overlaying each other.

```csharp
    [StructLayout(LayoutKind.Sequential)]
    public struct TopiValue : IDisposable, IEquatable<TopiValue>
    {
        [MarshalAs(UnmanagedType.U1)] public Tag tag;
        private TopiValueData _data;
        ...
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct TopiValueData
    {
        [FieldOffset(0)] [MarshalAs(UnmanagedType.I1)]
        public byte boolValue;

        [FieldOffset(0)] [MarshalAs(UnmanagedType.R4)]
        public float numberValue;

        [FieldOffset(0)] public IntPtr stringValue;

        [FieldOffset(0)] public TopiList listValue;
    }
```

This means you are required to check the `tag` field before accessing the data to know which field is currently active or use a switch statement.
If you prefer to accept an `object` you can use the `Value` property. Or if you're sure of the type (like in the Topi functions above)
you can use the named properties directly, just be careful. `Bool`, `Int`, `Float`, `String`, `List`, `Set`, `Map`.

```csharp
var value = new TopiValue(true);
value.Float // Incorrect property access, throws an error or returns malformed/incorrect data
value.String // Incorrect property access, throws an error or returns malformed/incorrect data
value.Bool // true
```

## EvtVariables

I've made [EvtVariables](https://github.com/peartreegames/evt-variables) the scriptableobject event system architecture a dependency of this package, though isn't actually necessary a lot of my tools use it. 
If you prefer not to keep it, please feel free to fork this repo and remove it.

EvtVariables are extents as EvtTopiVariables and used as runtime variable containers for any .topi `extern` variable.

Create one with `ContextMenu > Create > Evt > Topiary > [Type]`

Then set the `Topi Variable Name` to the name in the .topi file.

Topiary.Unity will automatically add the object to the Addressables `Topiary` group with labels `Topiary` and `Evt`.
Then each Conversation will automatically load the EvtVariables and hook up callbacks with the Topiary VM.