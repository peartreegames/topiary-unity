using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity.Editor
{
    [ScriptedImporter(1, "topi")]
    public class TopiScriptedImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = File.ReadAllText(ctx.assetPath);
            var asset = new TextAsset(text);
            var fileName = Path.GetFileName(ctx.assetPath);
            var icon = Resources.Load<Texture2D>("topi");
            var byteIcon = Resources.Load<Texture2D>("byte");
            ctx.AddObjectToAsset("main", asset, icon);
            ctx.SetMainObject(asset);
            try
            {
                var compiled = Story.Compile(text);
                var identifier = $"{fileName}b";
                var compiledAsset = ScriptableObject.CreateInstance<ByteData>();
                compiledAsset.name = identifier;
                compiledAsset.bytes = compiled;
                
                using var memStream = new MemoryStream(compiled);
                using var reader = new BinaryReader(memStream);
                compiledAsset.ExternsSet = ByteCode.GetExterns(reader);
                ctx.AddObjectToAsset(identifier, compiledAsset, byteIcon);

                var guid = AssetDatabase.GUIDFromAssetPath(ctx.assetPath);
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                settings.AddLabel("Topiary");
                settings.AddLabel("Topi");
                var group = settings.FindGroup("Topiary") ?? settings.CreateGroup("Topiary", false,
                    false,
                    false, settings.DefaultGroup.Schemas);
                var entry = settings.FindAssetEntry(guid.ToString());
                if (entry != null && entry.parentGroup == group) return;
                var addressable = settings.CreateOrMoveEntry(guid.ToString(), group);
                addressable.address = fileName; 
                addressable.SetLabel("Topiary", true);
                addressable.SetLabel("Topi", true);
                EditorUtility.SetDirty(settings);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}