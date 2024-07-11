using System;
using UnityEngine;
using UnityEngine.Events;

namespace PeartreeGames.Topiary.Unity
{
    public class TopiSpeaker : MonoBehaviour
    {
        [SerializeField] private new string name;
        [SerializeField] private UnityEvent<TopiSpeaker> onStartSpeaking;
        [SerializeField] private UnityEvent<TopiSpeaker> onStopSpeaking;
        public string Name => name;
        public UnityEvent<TopiSpeaker> OnStartSpeaking => onStartSpeaking;
        public UnityEvent<TopiSpeaker> OnStopSpeaking => onStopSpeaking;
        
        protected void Awake()
        {
            Dialogue.AddSpeaker(this);
        }

        protected void OnDestroy()
        {
            Dialogue.RemoveSpeaker(this);
        }

        public void StartSpeaking() => onStartSpeaking.Invoke(this);
        public void StopSpeaking() => onStopSpeaking.Invoke(this);

        private void OnValidate()
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "TopiSpeaker must have a name"); 
        }
    }
}