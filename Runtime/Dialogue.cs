using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Scripting;

namespace PeartreeGames.Topiary.Unity
{
    public class Dialogue : MonoBehaviour
    {
        [SerializeField, Tooltip("Default Starting Bough")]
        private string bough;

        [SerializeField] private string[] tags;
        [SerializeField] private AssetReferenceT<ByteData> file;
        [SerializeField] private Library.Severity logs = Library.Severity.Error;

        public ByteData Data { get; private set; }
        private Speaker _previousSpeaker;
        private GCHandle _pinnedHandle;
        private IntPtr _vmPtr;
        private Coroutine _playCoroutine;
        public string[] Tags => tags;

        public static event Func<Dialogue, IEnumerator> OnStart;
        public static event Action<Dialogue> OnEnd;
        public static event Action<Dialogue, Line, Speaker> OnLine;
        public static event Action<Dialogue, Choice[]> OnChoices;
        public static event Action<Dialogue, string, TopiValue> OnValueChanged;

#if ODIN_INSPECTOR
        [ShowInInspector]
#endif
        public static readonly State State = new();
        public static readonly Dictionary<string, Speaker> Speakers = new();
        public static readonly Dictionary<IntPtr, Dialogue> Dialogues = new();

        private static readonly List<TopiAttribute.FuncPtr> FunctionPtrs = new();

        private static Delegates.OnChoicesDelegate _onChoicesCallback;
        private static Delegates.OnLineDelegate _onLineCallback;
        private static Delegates.SubscriberDelegate _subscriberCallback;
        private static Delegates.OutputLogDelegate _onLogCallback;
        private static Delegates.FreeDelegate _freeCallback;
        private static Delegates.PanicHandlerDelegate _panicCallback;
        private static IntPtr _choicesPtr;
        private static IntPtr _linePtr;
        private static IntPtr _subscriberPtr;
        private static IntPtr _logPtr;
        private static IntPtr _freePtr;
        private static IntPtr _panicPtr;

        private bool IsVmValid
        {
            get
            {
                if (!_vmPtr.Equals(IntPtr.Zero)) return true;
                Log($"Invalid Vm: {name}", Library.Severity.Error);
                return false;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Init()
        {
            State.Clear();
            Speakers.Clear();
            Dialogues.Clear();
            FunctionPtrs.Clear();

            _onChoicesCallback = OnChoicesCallback;
            _onLineCallback = OnLineCallback;
            _onLogCallback = LogCallback;
            _subscriberCallback = ValueChangedCallback;
            _freeCallback = Free;
            _panicCallback = PanicCallback;

            _subscriberPtr = Marshal.GetFunctionPointerForDelegate(_subscriberCallback);
            _linePtr = Marshal.GetFunctionPointerForDelegate(_onLineCallback);
            _choicesPtr = Marshal.GetFunctionPointerForDelegate(_onChoicesCallback);
            _logPtr = Marshal.GetFunctionPointerForDelegate(_onLogCallback);
            _freePtr = Marshal.GetFunctionPointerForDelegate(_freeCallback);
            _panicPtr = Marshal.GetFunctionPointerForDelegate(_panicCallback);
            Library.setPanicHandler(_panicPtr);

            FunctionPtrs.AddRange(TopiAttribute.GetAllTopiMethodPtrs());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CleanupPointers()
        {
            foreach (var dialogue in new List<Dialogue>(Dialogues.Values))
            {
                if (dialogue != null) dialogue.Release();
            }
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
                Log($"{gameObject.name} no file set", Library.Severity.Warn);
                yield break;
            }

            var loc = Addressables.LoadResourceLocationsAsync(data);
            yield return loc;
            if (loc.Status != AsyncOperationStatus.Succeeded || loc.Result == null ||
                loc.Result.Count == 0)
            {
                Log($"{gameObject.name} no file found for {data.RuntimeKey}",
                    Library.Severity.Warn);
                yield break;
            }


            var ao = data.LoadAssetAsync<ByteData>();
            yield return ao;
            Data = ao.Result;
            if (Data == null)
            {
                Log($"{gameObject.name} ByteData could not be loaded",
                    Library.Severity.Error);
                yield break;
            }


            if (Data.bytes == null || Data.bytes.Length == 0)
            {
                Log($"{gameObject.name} ByteData has no bytes for {data.RuntimeKey}",
                    Library.Severity.Error);
                yield break;
            }

            _pinnedHandle = GCHandle.Alloc(Data.bytes, GCHandleType.Pinned);
            var sourcePtr = _pinnedHandle.AddrOfPinnedObject();
            _vmPtr = Library.createVm(sourcePtr, (UIntPtr)Data.bytes.Length, _linePtr, _choicesPtr,
                _subscriberPtr, _logPtr, logs);
            if (_vmPtr == IntPtr.Zero)
            {
                Log($"{gameObject.name} failed to create VM for {data.RuntimeKey}",
                    Library.Severity.Error);
                _pinnedHandle.Free();
                yield break;
            }
            Dialogues.Add(_vmPtr, this);
            LoadFunctions();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void Release()
        {
            if (_vmPtr != IntPtr.Zero)
            {
                Dialogues.Remove(_vmPtr);
                Library.destroyVm(_vmPtr);
                _vmPtr = IntPtr.Zero;
            }

            // 3. NOW it is safe to unpin and release assets
            if (_pinnedHandle.IsAllocated) _pinnedHandle.Free();
            if (file.IsValid()) file.ReleaseAsset();
        }

        public static void AddSpeaker(Speaker speaker) => Speakers[speaker.Id] = speaker;
        public static void RemoveSpeaker(Speaker speaker) => Speakers.Remove(speaker.Id);

        public void Continue()
        {
            if (IsVmValid) Library.selectContinue(_vmPtr);
        }

        public void SelectChoice(int index)
        {
            if (IsVmValid) Library.selectChoice(_vmPtr, (UIntPtr)index);
        }

        public void Play(string start = null)
        {
            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
            _playCoroutine = StartCoroutine(RunPlay(start));
        }

        private IEnumerator RunPlay(string start)
        {
            yield return PlayCoroutine(start);
            _playCoroutine = null;
        }

        public IEnumerator PlayCoroutine(string start = null)
        {
            if (!IsVmValid) yield break;
            SetState(State.Value);
            yield return null;

            if (OnStart != null)
            {
                var dels = OnStart.GetInvocationList();
                var coroutines = new Coroutine[dels.Length];
                for (var i = 0; i < dels.Length; i++)
                {
                    var handler = (Func<Dialogue, IEnumerator>)dels[i];
                    coroutines[i] = StartCoroutine(handler(this));
                }

                foreach (var co in coroutines) yield return co;
            }

            var startingBough = string.IsNullOrEmpty(start) ? bough : start;
            Library.start(_vmPtr, startingBough);
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
            }

            End();
        }

        public void Stop()
        {
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            End();
        }

        private void End()
        {
            var state = GetState();
            State.Amend(state);
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            _previousSpeaker = null;
            OnEnd?.Invoke(this);
        }

        private string GetState()
        {
            if (!IsVmValid) return null;
            var capacity = Library.calculateStateSize(_vmPtr);
            var output = new byte[(int)capacity];
            _ = Library.saveState(_vmPtr, output, (UIntPtr)output.Length);
            return Encoding.UTF8.GetString(output);
        }

        public void SetState(string json)
        {
            if (json != null && IsVmValid)
            {
                Library.loadState(_vmPtr, json, (UIntPtr)Encoding.UTF8.GetByteCount(json));
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

        [MonoPInvokeCallback(typeof(Delegates.OnLineDelegate)), Preserve]
        private static void OnLineCallback(IntPtr vmPtr, IntPtr linePtr)
        {
            try
            {
                if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
                {
                    Log($"Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                        Library.Severity.Error);
                    return;
                }

                var line = Marshal.PtrToStructure<LineNative>(linePtr).ToManaged();
                if (dialogue._previousSpeaker != null) dialogue._previousSpeaker.StopSpeaking();
                if (Speakers.TryGetValue(line.Speaker, out var speaker)) speaker.StartSpeaking();
                dialogue._previousSpeaker = speaker;
                OnLine?.Invoke(dialogue, line, speaker);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        [MonoPInvokeCallback(typeof(Delegates.OnChoicesDelegate)), Preserve]
        private static void OnChoicesCallback(IntPtr vmPtr, IntPtr choicesPtr, byte count)
        {
            try
            {
                if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
                {
                    Log($"Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                        Library.Severity.Error);
                    return;
                }

                OnChoices?.Invoke(dialogue, Choice.MarshalPtr(choicesPtr, count));
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        [MonoPInvokeCallback(typeof(Delegates.SubscriberDelegate)), Preserve]
        private static void ValueChangedCallback(IntPtr vmPtr, IntPtr namePtr, UIntPtr nameLen, TopiValue value)
        {
            try
            {
                var name = Marshal.PtrToStringUTF8(namePtr, (int)nameLen.ToUInt32());
                if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
                {
                    Log($"Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                        Library.Severity.Error);
                    return;
                }

                OnValueChanged?.Invoke(dialogue, name, value);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        [MonoPInvokeCallback(typeof(Delegates.OutputLogDelegate)), Preserve]
        private static void LogCallback(StringBuffer str, Library.Severity severity)
        {
            try { Log(str.Value, severity); }
            catch (Exception e) { Debug.LogException(e); }
        }

        [MonoPInvokeCallback(typeof(Delegates.FreeDelegate)), Preserve]
        internal static void Free(IntPtr ptr)
        {
            try { Marshal.FreeHGlobal(ptr); }
            catch (Exception e) { Debug.LogException(e); }
        }

        [MonoPInvokeCallback(typeof(Delegates.PanicHandlerDelegate)), Preserve]
        private static void PanicCallback(IntPtr msgPtr, UIntPtr msgLen)
        {
            // Process state is undefined here; keep this minimal — no allocations,
            // no Unity API beyond Debug.LogError, no re-entry into Topi.
            try
            {
                var msg = Marshal.PtrToStringUTF8(msgPtr, (int)msgLen.ToUInt32());
                Debug.LogError($"[Topiary] Native panic: {msg}");
            }
            catch { /* swallow — we're already in undefined state */ }
        }

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
                    Library.setExternFunc(_vmPtr, func.Name, TopiAttribute.TrampolinePtr,
                        func.Arity, func.UserData, _freePtr);
            }
        }
    }
}