using UnityEngine;
using UnityEngine.Events;

namespace PeartreeGames.Topiary.Unity
{
    public class TopiSpeaker : MonoBehaviour
    {
        public new string name;
        public UnityEvent<TopiSpeaker> onStartSpeaking;
        public UnityEvent<TopiSpeaker> onStopSpeaking;
        
        protected void Awake()
        {
            Conversation.AddSpeaker(this);
        }

        protected void OnDestroy()
        {
            Conversation.RemoveSpeaker(this);
        }

        public void StartSpeaking() => onStartSpeaking.Invoke(this);
        public void StopSpeaking() => onStopSpeaking.Invoke(this);
    }
}