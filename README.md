# Topiary.Unity

Unity Integration for the dialogue scripting tool [topiary](https://github.com/peartreegames/topiary).

Checkout the [syntax](https://peartree.games/topiary/docs/syntax) file if you're new to writing with Topi.

## Installation

Can be installed via the Package Manager > Add Package From Git URL...

This repo has an optional dependency on the EvtVariable package which can be installed. 
`https://github.com/peartreegames/evt-variables.git`

This package also depends on the Unity Addressables package, which should automatically be installed, but if you have any issues please install it manually from the Package Manager.

then the repo can be added

`https://github.com/peartreegames/topiary-unity.git`

## Setup

You'll need some sort of singleton DialogueRunner to go between Topiary and your UI.
Here's a rough sketch:

```csharp
public class DialogueRunner : MonoBehaviour
{
    private void Awake()
    {
        Dialogue.OnStart += OnStart;
        Dialogue.OnEnd += OnEnd;
        Dialogue.OnLine += OnLine;
        Dialogue.OnChoices += OnChoices;
    }

    private void OnDestroy()
    {
        Dialogue.OnStart -= OnStart;
        Dialogue.OnEnd -= OnEnd;
        Dialogue.OnLine -= OnLine;
        Dialogue.OnChoices -= OnChoices;
    }

    private void OnStart(Dialogue dialogue)
    {
        // Open/Update the UI
        // Perform any tasks for dialogue to start
    }

    private void OnLine(Dialogue dialogue, Line line, TopiSpeaker topiSpeaker)
    {
        // Update the UI to display the next line
        // Use dialogue.SelectContinue() to continue the script
    }

    private void OnChoices(Dialogue dialogue, Choice[] choices)
    {
        // Update the UI to display the choices
        // Use dialogue.SelectChoice(int) when the player makes their selection
    }

    private void OnEnd(Dialogue dialogue)
    {
        // Close/Update your UI
        // Perform any clean up tasks
        // Revert back to game mode
    }
}
```

## Setup

Any `.topi` file will automatically be compiled and converted into a `.topi.byte` bytecode.

Once your file is compiled it will automatically be added to a Topiary Addressables group with the labels `Topiary` and `Topi`.

Add a `Dialogue` MonoBehaviour to a GameObject and select the `.topi` file you want to associate with that Dialogue. 

Trigger the start of the Dialogue in any way you like with `dialogue.PlayDialogue()` or `StartCoroutine(dialogue.Play())`

## Functions

Topiary can call external functions that are marked with the `Topi` attribute.

Any **static** function with `IntPtr, byte` arguments and return type `TopiValue` is valid.
Originally functions were wrapped and allowed for `TopiValue` arguments to hide the IntPtr
and CreateArgs requirements, however Unity (the main use case for this package) will not work
with this work flow. So a more manual approach is required.

`MonoPInvokeCallback` is also required by Unity.

Here's some examples:

```csharp
public static class DialogueFunctions
{
    // playAnim will be replaced with the C# method.
    // We give the playAnim function a body in our .topi file for testing.
    // A warning will be shown if any extern isn't set when we start our Dialogue.
    // Preserve is used if code stripping is enabled
    // ex .topi file:
    //      extern const playAnim = |name, clip| {}
    //      playAnim("Player", "Laugh")
    [Topi("playAnim", 2)]
    [MonoPInvokeCallback(typeof(Delegates.ExternFunctionDelegate)), Preserve]
    public static TopiValue PlayAnim(IntPtr argsPtr, byte count)
    {
        var args = TopiValue.CreateArgs(argsPtr, count);
        var speakerName = args[0];
        var animClip = args[1];
        if (!Dialogue.Speakers.TryGetValue(speakerName.String, out var topi)) return default;
        // get Animator component and play clip
        return default;
    }
}
```

## TopiValue

I wanted to hide away the [TopiValue](https://github.com/peartreegames/topiary-unity/blob/main/Runtime/TopiValue.cs) implementation details, 
but without boxing everything to just `object` it didn't seem viable.

So instead here's your warning: TopiValues have data that are explicitly mapped out in memory 
and different fields are overlaying each other.

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
        [FieldOffset(0)] [MarshalAs(UnmanagedType.I1)] public byte boolValue;
        [FieldOffset(0)] [MarshalAs(UnmanagedType.R4)] public float numberValue;
        [FieldOffset(0)] public IntPtr stringValue;
        [FieldOffset(0)] public TopiList listValue;
        [FieldOffset(0)] public TopiEnum enumValue;
    }
```

This means you are required to check the `tag` field before accessing the data to know which field is currently active or use a switch statement.
If you prefer to accept an `object` you can use the `Value` property. Or if you're sure of the type (like in the Topi functions above)
you can use the named properties directly, just be careful. `Bool`, `Int`, `Float`, `String`, `List`, `Set`, `Map`, `Enum`.

```csharp
var value = new TopiValue(true);
value.Float // Incorrect property access, throws an error or returns malformed/incorrect data
value.String // Incorrect property access, throws an error or returns malformed/incorrect data
value.Bool // true
```

## EvtVariables

I've made [EvtTopiary](https://github.com/peartreegames/evt-topiary), a ScriptableObject event system architecture, an optional add-on.
You can also use the repo as an example of how you might want to connect Topiary variables to your own systems.
