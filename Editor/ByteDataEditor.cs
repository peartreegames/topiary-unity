using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PeartreeGames.Topiary.Unity.Editor
{
    [CustomEditor(typeof(ByteData))]
    public class ByteDataEditor : UnityEditor.Editor
    {
        private ByteData _byteData;
        private SerializedProperty _externsProperty;

        private void OnEnable()
        {
            _byteData = (ByteData) target;
            _externsProperty = serializedObject.FindProperty("externs");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var elem = new VisualElement();

            var externsField = new PropertyField(_externsProperty);
            externsField.SetEnabled(false);
            elem.Add(externsField); 
            var d = _byteData.bytes;
            elem.Add(new Label($"Compiled {d.Length}\n"));
            var text = BitConverter.ToString(d)[..Mathf.Min(d.Length + 1, 4001)];
            if (d.Length > 4001) text += "...";
            var dataLabel = new Label(text)
                {
                    style =
                    {
                        whiteSpace = WhiteSpace.Normal
                    }
                };
            elem.Add(dataLabel);
            return elem;
        }
    }
}