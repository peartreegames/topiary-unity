using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    public class State
    {
        private JObject _rootState;
        [ShowInInspector, TextArea]
        public string Value
        {
            get => _rootState?.ToString();
            set => _rootState = new JObject(value);
        }

        /// <summary>
        /// Add current Dialogue values to the State
        /// </summary>
        /// <param name="dialogue"></param>
        public void Amend(Dialogue dialogue)
        {
            var state = dialogue.SaveState();
            if (_rootState == null) _rootState = JObject.Parse(state);
            else
            {
                var jObj = JObject.Parse(state);
                foreach (var item in jObj) _rootState[item.Key] = item.Value;
            }
        }

        /// <summary>
        /// Load the current State into the Dialogue
        /// </summary>
        /// <param name="dialogue"></param>
        public void Inject(Dialogue dialogue)
        {
            if (_rootState == null) return;
            dialogue.LoadState(Value); 
        }
    }
}