using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AOT;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Scripting;

namespace PeartreeGames.Topiary.Unity
{
    public class Dialogue : MonoBehaviour
    {
        [SerializeField] private string bough;
        [SerializeField] private string[] tags;
        [SerializeField] private AssetReferenceT<ByteData> file;
        [SerializeField] private Library.Severity logs = Library.Severity.Error;

        public ByteData Data { get; private set; }
        private Speaker _previousSpeaker;
        private GCHandle _pinnedHandle;
        private IntPtr _vmPtr;
        public string[] Tags => tags;

        public static event Action<Dialogue> OnStart;
        public static event Func<Dialogue, Task> OnStartBlocking;
        public static event Action<Dialogue> OnEnd;
        public static event Action<Dialogue, Line, Speaker> OnLine;
        public static event Action<Dialogue, Choice[]> OnChoices;
        public static event Action<Dialogue, string, TopiValue> OnValueChanged;

        [ShowInInspector] public static readonly State State = new();
        public static readonly Dictionary<string, Speaker> Speakers = new();
        public static readonly Dictionary<IntPtr, Dialogue> Dialogues = new();

        private static readonly List<TopiAttribute.FuncPtr> FunctionPtrs = new();

        private static Delegates.OnChoicesDelegate _onChoicesCallback;
        private static Delegates.OnLineDelegate _onLineCallback;
        private static Delegates.SubscriberDelegate _subscriberCallback;
        private static Delegates.OutputLogDelegate _onLogCallback;
        private static Delegates.FreeDelegate _freeCallback;
        private static IntPtr _choicesPtr;
        private static IntPtr _linePtr;
        private static IntPtr _subscriberPtr;
        private static IntPtr _logPtr;
        private static IntPtr _freePtr;

        private bool IsVmValid
        {
            get
            {
                if (!_vmPtr.Equals(IntPtr.Zero)) return true;
                Log($"Invalid Vm: {name}", Library.Severity.Error);
                return false;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            State.Clear();
            Speakers.Clear();
            Dialogues.Clear();
            FunctionPtrs.Clear();
            _onChoicesCallback ??= OnChoicesCallback;
            _onLineCallback ??= OnLineCallback;
            _onLogCallback ??= LogCallback;
            _subscriberCallback ??= ValueChangedCallback;
            _freeCallback ??= Free;
            _subscriberPtr = Marshal.GetFunctionPointerForDelegate(_subscriberCallback);
            _linePtr = Marshal.GetFunctionPointerForDelegate(_onLineCallback);
            _choicesPtr = Marshal.GetFunctionPointerForDelegate(_onChoicesCallback);
            _logPtr = Marshal.GetFunctionPointerForDelegate(_onLogCallback);
            _freePtr = Marshal.GetFunctionPointerForDelegate(_freeCallback);
            FunctionPtrs.AddRange(TopiAttribute.GetAllTopiMethodPtrs());
        }

        private IEnumerator Start()
        {
            yield return StartCoroutine(SetFile(file));
        }

        public IEnumerator SetFile(AssetReferenceT<ByteData> data)
        {
            Release();
            if (data == null)
            {
                Log($"No file set: {gameObject}", Library.Severity.Error);
                yield break;
            }

            var loc = Addressables.LoadResourceLocationsAsync(data);
            yield return loc;
            if (loc.Status != AsyncOperationStatus.Succeeded || loc.Result == null ||
                loc.Result.Count == 0)
            {
                Log($"No file set: {data}", Library.Severity.Error);
                yield break;
            }


            var ao = data.LoadAssetAsync<ByteData>();
            yield return ao;
            Data = ao.Result;
            if (Data == null)
            {
                Log($"ByteData could not be loaded: {data}",
                    Library.Severity.Error);
                yield break;
            }


            _pinnedHandle = GCHandle.Alloc(Data.bytes, GCHandleType.Pinned);
            var sourcePtr = _pinnedHandle.AddrOfPinnedObject();
            _vmPtr = Library.createVm(sourcePtr, Data.bytes.Length, _linePtr, _choicesPtr,
                _subscriberPtr, _logPtr, logs);
            Dialogues.Add(_vmPtr, this);
        }

        private void OnDestroy()
        {
            Release();
        }

        private void Release()
        {
            if (_pinnedHandle.IsAllocated) _pinnedHandle.Free();
            if (file.IsValid()) file.ReleaseAsset();

            if (_vmPtr == IntPtr.Zero) return;
            Dialogues.Remove(_vmPtr);
            Library.destroyVm(_vmPtr);
            _vmPtr = IntPtr.Zero;
        }

        public static void AddSpeaker(Speaker speaker) => Speakers[speaker.Id] = speaker;
        public static void RemoveSpeaker(Speaker speaker) => Speakers.Remove(speaker.Id);

        public void Continue()
        {
            if (IsVmValid) Library.selectContinue(_vmPtr);
        }

        public void SelectChoice(int index)
        {
            if (IsVmValid) Library.selectChoice(_vmPtr, index);
        }

        public void PlayDialogue() => StartCoroutine(Play());

        public IEnumerator Play()
        {
            if (!IsVmValid) yield break;
            SetState(State.Value);
            LoadFunctions();
            yield return null;

            if (OnStartBlocking != null)
            {
                var dels = OnStartBlocking.GetInvocationList();
                var tasks = new Task[dels.Length];
                for (var i = 0; i < dels.Length; i++)
                {
                    var handler = (Func<Dialogue, Task>)dels[i];
                    tasks[i] = handler(this);
                }
                yield return new WaitUntil(() => Task.WhenAll(tasks).IsCompleted);
            }
            
            OnStart?.Invoke(this);
            Library.start(_vmPtr, bough);
            while (Library.canContinue(_vmPtr))
            {
                try
                {
                    Library.run(_vmPtr);
                }
                catch (SEHException ex)
                {
                    Log($"SEHException: {ex}", Library.Severity.Error);
                    break;
                }
                catch (Exception e)
                {
                    Log($"Exception: {e}", Library.Severity.Error);
                    break;
                }

                while (Library.isWaiting(_vmPtr)) yield return null;
                Debug.Log("Dialogue Next");
            }

            End();
        }

        public void Stop()
        {
            StopAllCoroutines();
            End();
        }

        private void End()
        {
            var state = GetState();
            State.Amend(state);
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            _previousSpeaker = null;
            OnEnd?.Invoke(this);
#if EVT_TOPIARY
            _registrar.UnloadTopiValues();
#endif
        }

        private string GetState()
        {
            if (!IsVmValid) return null;
            var capacity = Library.calculateStateSize(_vmPtr);
            var output = new byte[capacity];
            if (!IsVmValid) return null;
            _ = Library.saveState(_vmPtr, output, output.Length);
            return Encoding.UTF8.GetString(output);
        }

        public void SetState(string json)
        {
            if (json != null && IsVmValid)
            {
                Library.loadState(_vmPtr, json, json.Length);
            }
        }

        public bool Subscribe(string variableName) =>
            IsVmValid && Library.subscribe(_vmPtr, variableName);

        /// <summary>
        /// Unsubscribe when a Global variable changes
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        public bool Unsubscribe(string variableName) =>
            IsVmValid && Library.unsubscribe(_vmPtr, variableName);

        /// <summary>
        /// Set an Extern variable to a TopiValue
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        /// <param name="value">The value to set</param>
        public void Set(string variableName, TopiValue value)
        {
            if (!IsVmValid) return;
            if (!Data.Externs.Contains(variableName))
            {
                Log($"{Data.name} does not contain a variable '{variableName}'",
                    Library.Severity.Warn);
                return;
            }

            Library.setExtern(_vmPtr, variableName, value, _freePtr);
        }

        [MonoPInvokeCallback(typeof(Delegates.OnLineDelegate)), Preserve]
        private static void OnLineCallback(IntPtr vmPtr, Line line)
        {
            if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
            {
                Log($"Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                    Library.Severity.Error);
                return;
            }

            if (dialogue._previousSpeaker != null) dialogue._previousSpeaker.StopSpeaking();
            if (Speakers.TryGetValue(line.Speaker, out var speaker)) speaker.StartSpeaking();
            dialogue._previousSpeaker = speaker;
            OnLine?.Invoke(dialogue, line, speaker);
        }

        [MonoPInvokeCallback(typeof(Delegates.OnChoicesDelegate)), Preserve]
        private static void OnChoicesCallback(IntPtr vmPtr, IntPtr choicesPtr, byte count)
        {
            if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
            {
                Log($"Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                    Library.Severity.Error);
                return;
            }

            OnChoices?.Invoke(dialogue, Choice.MarshalPtr(choicesPtr, count));
        }

        [MonoPInvokeCallback(typeof(Delegates.SubscriberDelegate)), Preserve]
        private static void ValueChangedCallback(IntPtr vmPtr, string name, TopiValue value)
        {
            if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
            {
                Log($"Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                    Library.Severity.Error);
                return;
            }

#if EVT_TOPIARY
            dialogue._registrar.OnValueChanged(name, value);
#endif
            OnValueChanged?.Invoke(dialogue, name, value);
        }

        [MonoPInvokeCallback(typeof(Delegates.OutputLogDelegate)), Preserve]
        private static void LogCallback(StringBuffer str, Library.Severity severity) =>
            Log(str.Value, severity);

        [MonoPInvokeCallback(typeof(Delegates.FreeDelegate)), Preserve]
        internal static void Free(IntPtr ptr) => Marshal.FreeHGlobal(ptr);

        public static void Log(string msg, Library.Severity severity)
        {
            const string prefix = "[Topiary] ";
            switch (severity)
            {
                case Library.Severity.Debug:
                case Library.Severity.Info:
                    Debug.Log(prefix + msg);
                    break;
                case Library.Severity.Warn:
                    Debug.LogWarning(prefix + msg);
                    break;
                case Library.Severity.Error:
                    Debug.LogError(prefix + msg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }

        private void LoadFunctions()
        {
            foreach (var func in FunctionPtrs)
            {
                if (Data.Externs.Contains(func.Name))
                    Library.setExternFunc(_vmPtr, func.Name, func.Ptr, func.Arity, _freePtr);
            }
        }
    }
}