using UnityEditor;
using UnityEditor.AddressableAssets;

namespace PeartreeGames.Topiary.Unity.Editor
{
    public static class AddressablesSetup
    {
        [MenuItem("Tools/Evt/Setup Addressables for EvtTopiVariables")]
        public static void SetupEvtTopiValuesInAddressables()
        {
            SetupEvtTopiVariable<EvtTopiBool, bool>();
            SetupEvtTopiVariable<EvtTopiInt, int>();
            SetupEvtTopiVariable<EvtTopiFloat, float>();
            SetupEvtTopiVariable<EvtTopiString, string>();
            SetupEvtTopiVariable<EvtTopiEnum, string>();
        }

        private static void SetupEvtTopiVariable<T, TU>() where T : EvtTopiVariable<TU>
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.AddLabel("Topiary");
            settings.AddLabel("Evt");

            var group = settings.FindGroup("Topiary") ?? settings.CreateGroup("Topiary", false,
                false,
                false, settings.DefaultGroup.Schemas);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var evtTopiValue = AssetDatabase.LoadAssetAtPath<T>(path);
                var name = evtTopiValue.Name; 
                if (string.IsNullOrEmpty(name)) continue;

                var addressable = settings.CreateOrMoveEntry(guid, group);
                addressable.address = name;
                addressable.SetLabel("Topiary", true);
                addressable.SetLabel("Evt", true);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}