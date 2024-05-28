using System;
using System.Collections;
using System.Collections.Generic;
using PeartreeGames.Evt.Variables;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PeartreeGames.Topiary.Unity
{
    public class Conversation : MonoBehaviour
    {
        [SerializeField] private string bough;
        [SerializeField] private string[] tags;
        [SerializeField] private AssetReferenceT<ByteData> file;
        [SerializeField] private Library.Severity logs = Library.Severity.Error;
        
        private ByteData _data;

        public Dialogue Dialogue { get; private set; }
        public string[] Tags => tags;

        private TopiSpeaker _previousSpeaker;
        public static event Action<Dialogue, Conversation> OnStart;
        public static event Action<Dialogue, Conversation> OnEnd;
        public static event Action<Dialogue, Line, TopiSpeaker> OnLine;
        public static event Action<Dialogue, Choice[]> OnChoices;
        private static Dictionary<string, EvtVariable> Variables = new();
        public static Dictionary<string, TopiSpeaker> Speakers { get; } = new();
        public static void AddSpeaker(TopiSpeaker speaker) => Speakers[speaker.name] = speaker;
        public static void RemoveSpeaker(TopiSpeaker speaker) => Speakers.Remove(speaker.name);

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
            try
            {
                Dialogue = new Dialogue(_data.bytes, OnLineCallback, OnChoicesCallback, logs);
                if (!Dialogue.IsValid) Log("[Topiary.Unity] Could not create Dialogue ", Library.Severity.Error);
                else Dialogue.BindFunctions(AppDomain.CurrentDomain.GetAssemblies());
            }
            finally
            {
                Library.OnDebugLogMessage -= Log;
            }
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

            Dialogue.Library.SetSubscribeCallback(ValueChanged);
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

        [MonoPInvokeCallback(typeof(Delegates.Subscriber))]
        public static void ValueChanged(string name, ref TopiValue value)
        {
            if (Variables.TryGetValue(name, out var variable))
            {
                switch (value.Tag)
                {
                    case TopiValue.tag.Bool when variable is EvtTopiBool b:
                        b.Value = value.Bool;
                        break;
                    case TopiValue.tag.Int when variable is EvtTopiInt i:
                        i.Value = value.Int;
                        break;
                    case TopiValue.tag.Float when variable is EvtTopiFloat f:
                        f.Value = value.Float;
                        break;
                    case TopiValue.tag.String when variable is EvtTopiString s:
                        s.Value = value.String;
                        break;
                }
            }
        }
        
        private IEnumerator LoadAddressableTopiValues()
        {
            var ao = Addressables.LoadResourceLocationsAsync(new List<string> {"Topiary", "Evt"},
                Addressables.MergeMode.Intersection);
            yield return ao;
            var list = ao.Result;
            foreach (var item in list)
            {
                var key = item.PrimaryKey;
                if (!_data.ExternsSet.Contains(key)) continue;
                var aoEvt = Addressables.LoadAssetAsync<EvtVariable>(key);
                yield return aoEvt;
                
                string name = aoEvt.Result switch
                {
                    EvtTopiBool b => b.Name,
                    EvtTopiFloat f => f.Name,
                    EvtTopiInt i => i.Name,
                    EvtTopiString s => s.Name,
                    _ => null
                };
                if (evtRef == null)
                {
                    Addressables.ReleaseAsset(aoEvt);
                    continue;
                }
                Variables[name] = aoEvt.Result;
            }
        }

        private void UnloadAddressableTopiValues()
        {
            foreach (var kvp in Variables)
            {
                Addressables.ReleaseAsset(kvp.Value);
            }
        }
    }
}