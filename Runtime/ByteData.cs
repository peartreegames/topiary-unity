using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    /// <summary>
    /// Represents a class that holds byte data and a set of external references.
    /// </summary>
    public class ByteData : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Represents a byte array.
        /// </summary>
        public byte[] bytes;

        /// <summary>
        /// Represents a byte data object that can be serialized and deserialized.
        /// </summary>
        [SerializeField] private string[] externs;

        /// of strings, while the `OnAfterDeserialize` method recreates the `ExternsSet` from the deserialized array.
        public SortedSet<string> ExternsSet;

        public void OnBeforeSerialize() => externs = ExternsSet?.ToArray();

        public void OnAfterDeserialize() => ExternsSet = new SortedSet<string>(externs);
    }
}