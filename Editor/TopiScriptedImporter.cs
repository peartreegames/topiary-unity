using System;
using System.IO;
using System.Text;
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
            using var streamReader = new StreamReader(ctx.assetPath, Encoding.UTF8);
            var text = streamReader.ReadToEnd();
            var asset = new TextAsset(text);
            var fileName = Path.GetFileName(ctx.assetPath);
            var icon = Resources.Load<Texture2D>("topi");
            var byteIcon = Resources.Load<Texture2D>("byte");
            ctx.AddObjectToAsset("main", asset, icon);
            ctx.SetMainObject(asset);
            Library.OnDebugLogMessage += Log;
            try
            {
                var absPath = Application.dataPath + ctx.assetPath[6..];
                var compiled = Story.Compile(absPath);
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
            Library.OnDebugLogMessage -= Log;
        }
        public void Log(string msg, Library.Severity severity)
        {
            switch (severity)
            {
                case Library.Severity.Debug:
                case Library.Severity.Info:
                    Debug.Log(msg);
                    break;
                case Library.Severity.Warn:
                    Debug.LogWarning(msg);
                    break;
                case Library.Severity.Error:
                    Debug.LogError(msg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }
    }
}