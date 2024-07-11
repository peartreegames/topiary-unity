using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector;

namespace PeartreeGames.Topiary.Unity
{
    public class State
    {
        private JObject _rootState;
        [ShowInInspector]
        public string Value
        {
            get => _rootState?.ToString();
            set => _rootState = new JObject(value);
        }

        /// <summary>
        /// Add current JSON values to the State
        /// </summary>
        /// <param name="jsonString"></param>
        public void Amend(string jsonString)
        {
            if (_rootState == null) _rootState = JObject.Parse(jsonString);
            else
            {
                var jObj = JObject.Parse(jsonString);
                foreach (var item in jObj) _rootState[item.Key] = item.Value;
            }
        }
    }
}