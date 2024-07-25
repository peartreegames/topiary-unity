using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PeartreeGames.Topiary.Unity.Editor
{
    [ScriptedImporter(1, "topi")]
    public class TopiScriptedImporter : ScriptedImporter
    {
        [SerializeField] private Library.Severity severity = Library.Severity.Error;
        public override void OnImportAsset(AssetImportContext ctx)
        {
            using var streamReader = new StreamReader(ctx.assetPath, Encoding.UTF8);
            var text = streamReader.ReadToEnd();
            var fileName = Path.GetFileName(ctx.assetPath);
            var icon = Resources.Load<Texture2D>("topi");
            var byteIcon = Resources.Load<Texture2D>("byte");
            if (string.IsNullOrEmpty(text))
            {
                var empty = new TextAsset(text);
                ctx.AddObjectToAsset("main", empty, icon);
                ctx.SetMainObject(empty);
                return;
            }
            
            var log = Logger(ctx);
            var logPtr =  Marshal.GetFunctionPointerForDelegate(log);
            Object asset;
            try
            {
                var absPath = Application.dataPath[..^6] + ctx.assetPath;
                var size = Library.calculateCompileSize(absPath, logPtr, severity);
                var output = new byte[size];
                _ = Library.compile(absPath, output, size, logPtr, severity);
                
                using var memStream = new MemoryStream(output);
                using var reader = new BinaryReader(memStream);
                var boughs = ByteData.GetBoughs(reader);
                if (boughs.Length == 0)
                {
                    var empty = new TextAsset(text);
                    ctx.AddObjectToAsset("main", empty, icon);
                    ctx.SetMainObject(empty);
                    return;
                }

                var identifier = $"{fileName}.byte";
                asset = ScriptableObject.CreateInstance<ByteData>();
                asset.name = identifier;
                ((ByteData)asset).bytes = output;

                reader.BaseStream.Position = 0;
                ((ByteData)asset).ExternsSet = ByteData.GetExterns(reader);
                ctx.AddObjectToAsset("main", asset, byteIcon);
                ctx.SetMainObject(asset);
                
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
            catch (EndOfStreamException)
            {
                asset = new TextAsset(text);
                icon = Resources.Load<Texture2D>("error");
                ctx.AddObjectToAsset("main", asset, icon);
                ctx.SetMainObject(asset);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private static Delegates.OutputLogDelegate Logger(AssetImportContext ctx) =>
            (str, severity) =>
            {
                var msg = str.Value;
                switch (severity)
                {
                    case Library.Severity.Debug:
                        break;
                    case Library.Severity.Info:
                        Debug.Log(msg);
                        break;
                    case Library.Severity.Warn:
                        ctx.LogImportWarning(msg);
                        break;
                    case Library.Severity.Error:
                        ctx.LogImportError(msg);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
                }
            };
    }
}