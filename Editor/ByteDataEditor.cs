using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PeartreeGames.Topiary.Unity.Editor
{
    [CustomEditor(typeof(ByteData))]
    public class ByteDataEditor : UnityEditor.Editor
    {
        private ByteData _byteData;
        private SerializedProperty _externsProperty;
        private string _text;

        private void OnEnable()
        {
            _byteData = (ByteData) target;
            _externsProperty = serializedObject.FindProperty("externs");
            using var streamReader = new StreamReader(AssetDatabase.GetAssetPath(target), Encoding.UTF8);
            _text = streamReader.ReadToEnd();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var elem = new VisualElement();
            
            var d = _byteData.bytes;
            elem.Add(new Label($"Compiled: {d.Length:N0} bytes\n"));
            
            var externsField = new PropertyField(_externsProperty);
            externsField.SetEnabled(false);
            elem.Add(externsField); 
            
            var textField = new TextField
            {
                isReadOnly = true,
                value = _text
            };
            elem.Add(textField);
            
            return elem;
        }
    }
}