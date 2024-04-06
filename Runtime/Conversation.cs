using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PeartreeGames.Evt.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PeartreeGames.Topiary.Unity
{
    public class Conversation : MonoBehaviour
    {
        public static event Action<Dialogue, Conversation> OnStart;
        public static event Action<Dialogue, Conversation> OnEnd;
        public static event Action<Dialogue, Line, TopiSpeaker> OnLine;
        public static event Action<Dialogue, Choice[]> OnChoices;

        [SerializeField] private string bough;
        [SerializeField] private string[] tags;
        [SerializeField] private AssetReferenceT<ByteData> file;
        private ByteData _data;

        [SerializeField] private Library.Severity logs = Library.Severity.Error;
        public Dialogue Dialogue { get; private set; }
        public string[] Tags => tags;

        private TopiSpeaker _previousSpeaker;
        private List<EvtTopiReference> _evtReferences;

        public static Dictionary<string, TopiSpeaker> Speakers { get; } = new();
        public static void AddSpeaker(TopiSpeaker speaker) => Speakers[speaker.name] = speaker;
        public static void RemoveSpeaker(TopiSpeaker speaker) => Speakers.Remove(speaker.name);

        [ShowInInspector]
        public static readonly State State = new();
        private IEnumerator Start()
        {
            if (file == null)
            {
                Log($"[Topiary.Unity] No file set: {file}", Library.Severity.Error);
                yield break;
            }
            var loc = Addressables.LoadResourceLocationsAsync(file);
            yield return loc;
            if (loc.Status != AsyncOperationStatus.Succeeded || loc.Result == null || loc.Result.Count == 0)
            {
                Log($"[Topiary.Unity] No file set: {file}", Library.Severity.Error);
                yield break;
            }
            

            var ao = Addressables.LoadAssetAsync<ByteData>(file);
            yield return ao;
            _data = ao.Result;
            Library.OnDebugLogMessage += Log;
            Dialogue = new Dialogue(_data.bytes, OnLineCallback, OnChoicesCallback, logs);
            if (!Dialogue.IsValid) Log("[Topiary.Unity] Could not create Dialogue ", Library.Severity.Error);
            else Dialogue.BindFunctions(AppDomain.CurrentDomain.GetAssemblies());
            Library.OnDebugLogMessage -= Log;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            Library.IsUnityRuntime = true;
        }

        public void Log(string msg, Library.Severity severity)
        {
            switch (severity)
            {
                case Library.Severity.Debug:
                case Library.Severity.Info:
                    Debug.Log($"{name}: {msg}", gameObject);
                    break;
                case Library.Severity.Warn:
                    Debug.LogWarning($"{name}: {msg}", gameObject);
                    break;
                case Library.Severity.Error:
                    Debug.LogError($"{name}: {msg}", gameObject);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }

        private void OnDestroy()
        {
            Library.OnDebugLogMessage -= Log;
            UnloadAddressableTopiValues();
            Dialogue?.Dispose();
            if (file.IsValid()) file.ReleaseAsset();
        }

        private void OnLineCallback(Dialogue dialogue, Line line)
        {
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            if (Speakers.TryGetValue(line.Speaker, out var speaker)) speaker.StartSpeaking();
            _previousSpeaker = speaker;
            OnLine?.Invoke(dialogue, line, speaker);
        }

        private void OnChoicesCallback(Dialogue dialogue, Choice[] choices) =>
            OnChoices?.Invoke(dialogue, choices);

        public void PlayDialogue()
        {
            StartCoroutine(Play());
        }

        public IEnumerator Play()
        {
            if (!Dialogue.IsValid)
            {
                Log("[Topiary.Unity] Invalid Dialogue", Library.Severity.Warn);
                yield break;
            }
            Dialogue.Library.SetDebugSeverity(logs);
            Library.OnDebugLogMessage += Log;
            yield return StartCoroutine(LoadAddressableTopiValues());
            State.Inject(Dialogue);
            OnStart?.Invoke(Dialogue, this);
            Dialogue.Start(bough);
            while (Dialogue?.CanContinue ?? false)
            {
                try
                {
                    Dialogue?.Run();
                }
                catch (System.Runtime.InteropServices.SEHException ex)
                {
                    Log($"Caught an SEHException: {ex}", Library.Severity.Error);
                    break;
                }
                catch (Exception e)
                {
                    Log(e.Message, Library.Severity.Error);
                    break;
                }

                while (Dialogue?.IsWaiting ?? false) yield return null;
            }

            State.Amend(Dialogue);
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            _previousSpeaker = null;
            OnEnd?.Invoke(Dialogue, this);
            UnloadAddressableTopiValues();
            Library.OnDebugLogMessage -= Log;
        }


        private IEnumerator LoadAddressableTopiValues()
        {
            var ao = Addressables.LoadResourceLocationsAsync(new List<string> {"Topiary", "Evt"},
                Addressables.MergeMode.Intersection);
            yield return ao;
            var list = ao.Result;
            _evtReferences = new List<EvtTopiReference>();
            foreach (var item in list)
            {
                var key = item.PrimaryKey;
                if (!_data.ExternsSet.Contains(key)) continue;
                var aoEvt = Addressables.LoadAssetAsync<EvtVariable>(key);
                yield return aoEvt;

                EvtTopiReference evtRef = aoEvt.Result switch
                {
                    EvtTopiBool b => new EvtBoolReference(this, b),
                    EvtTopiFloat f => new EvtFloatReference(this, f),
                    EvtTopiInt i => new EvtIntReference(this, i),
                    EvtTopiString s => new EvtStringReference(this, s),
                    _ => null
                };
                if (evtRef == null) continue;
                _evtReferences.Add(evtRef);
            }
        }

        private void UnloadAddressableTopiValues()
        {
            if (_evtReferences == null) return;
            foreach (var reference in _evtReferences) reference?.Dispose();
            _evtReferences.Clear();
        }
    }
}