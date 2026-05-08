using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    public class State
    {
        private JObject _rootState;
        public string Value => _rootState?.ToString();

        /// <summary>
        /// Add current JSON values to the State
        /// </summary>
        /// <param name="jsonString"></param>
        public void Amend(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString)) return;
            try
            {
                if (_rootState == null) _rootState = JObject.Parse(jsonString);
                else
                {
                    var jObj = JObject.Parse(jsonString);
                    foreach (var item in jObj) _rootState[item.Key] = item.Value;
                }
            }
            catch (JsonException e)
            {
                Debug.LogError($"[Topiary] State.Amend failed to parse JSON: {e.Message}");
            }
        }

        public void Set(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString)) return;
            try
            {
                _rootState = JObject.Parse(jsonString);
            }
            catch (JsonException e)
            {
                Debug.LogError($"[Topiary] State.Set failed to parse JSON: {e.Message}");
            }
        }

        public void Clear() => _rootState = new JObject();
    }
}