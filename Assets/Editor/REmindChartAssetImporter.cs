using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

/// <summary>
/// Keeps the editable .rd file as the single chart source while making its text
/// available to a built Player through a normal TextAsset reference.
/// </summary>
[ScriptedImporter(1, "rd")]
public sealed class REmindChartAssetImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext context)
    {
        string text = File.ReadAllText(
            context.assetPath,
            new UTF8Encoding(false, true));
        var asset = new TextAsset(text)
        {
            name = Path.GetFileNameWithoutExtension(context.assetPath)
        };
        context.AddObjectToAsset("chart", asset);
        context.SetMainObject(asset);
    }
}
