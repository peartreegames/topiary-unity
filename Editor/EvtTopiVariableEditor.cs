using System.Linq;
using PeartreeGames.Evt.Variables.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PeartreeGames.Topiary.Unity.Editor
{
    [CustomEditor(typeof(EvtTopiVariable<>), true)]
    public class EvtTopiVariableEditor : EvtVariableEditor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var elem = base.CreateInspectorGUI();
            var valueNameProp = serializedObject.FindProperty("valueName");
            var valueName = valueNameProp.stringValue;
            AddressableAssetEntry addEntry = null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                foreach (var group in settings.groups.Where(group => group.Name == "Topiary"))
                {
                    foreach (var entry in group.entries)
                    {
                        if (entry.address != valueName) continue;
                        addEntry = entry;
                        break;
                    }
                }
            }

            var field = new PropertyField(valueNameProp){ label = "Topi Variable Name"};
            field.BindProperty(serializedObject);
            field.RegisterValueChangeCallback(v =>
            {
                var newName = v.changedProperty.stringValue;
                addEntry?.SetAddress(newName);
            });
            elem.Add(field);
            if (target is EvtTopiEnum)
            {
                var enumProp = serializedObject.FindProperty("topiEnum");
                var enumField = new PropertyField(enumProp);
                elem.Add(enumField);
                elem.Remove(elem.Q<PropertyField>("value"));
            }
            elem.Insert(0, new Button(AddressablesSetup.SetupEvtTopiValuesInAddressables){ text = "Set Addressables" });
            return elem;
        }
    }
}