using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxPaletteAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxPaletteExpedition.unity";
        public const string PalettePath = "Assets/PAX/Generation/ConstructionPalette.asset";
        public const string UiPath = "Assets/PAX/UI/PaletteExpedition.uxml";
        [MenuItem("PAX/Create material palette expedition if missing")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreatePaletteExpedition();
        }
        public static void CreatePaletteExpedition()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author in Edit Mode only.");
            if (File.Exists(ScenePath)) { Debug.Log("PAX palette expedition exists; preserving scene and saves."); return; }
            var scene = EditorSceneManager.OpenScene(PaxExpeditionAuthoring.ScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not create palette scene.");
            var palette = AssetDatabase.LoadAssetAtPath<PaxMaterialPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<PaxMaterialPalette>();
                string[] ids = { "default", "timber", "stone", "earth", "roof" }, titles = { "Grass", "Timber", "Stone", "Earth", "Roof" }, materials = { "Grass", "Timber", "Rock", "Earth", "Roof" };
                palette.Configure(Enumerable.Range(0, ids.Length).Select(i => new PaxMaterialPalette.Entry
                { id = ids[i], title = titles[i], material = AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/" + materials[i] + ".mat") }).ToArray());
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            if (!palette.Validate(out string reason)) throw new InvalidOperationException(reason);
            if (!File.Exists(UiPath))
            {
                string markup = File.ReadAllText("Assets/PAX/UI/Expedition.uxml");
                markup = markup.Replace("</ui:UXML>", "    <ui:VisualElement style=\"position: absolute; left: 16px; bottom: 16px; width: 610px; padding-left: 14px; padding-right: 14px; padding-top: 10px; padding-bottom: 10px; background-color: rgba(12, 21, 31, 0.93);\" picking-mode=\"Ignore\">\n        <ui:Label name=\"construction-palette\" text=\"1 Grass · 2 Timber · 3 Stone · 4 Earth · 5 Roof\" style=\"font-size: 14px; color: rgb(235, 242, 225); white-space: normal;\" picking-mode=\"Ignore\" />\n    </ui:VisualElement>\n</ui:UXML>");
                File.WriteAllText(UiPath, markup); AssetDatabase.ImportAsset(UiPath);
            }
            var world = Object.FindFirstObjectByType<PaxWorld>();
            var builder = Object.FindFirstObjectByType<PaxBuilder>();
            var document = Object.FindFirstObjectByType<UIDocument>();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);
            var sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sample.name = "Selected material preview sample"; sample.layer = 2;
            Object.DestroyImmediate(sample.GetComponent<Collider>());
            sample.transform.SetParent(builder.PlacementPreview, false); sample.transform.localScale = Vector3.one * 0.78f;
            var visual = sample.GetComponent<Renderer>(); visual.sharedMaterial = palette.Entries[0].material;
            visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
            var selection = new GameObject("Editable construction palette").AddComponent<PaxMaterialSelection>();
            selection.Configure(world, builder, palette, visual, document);
            var save = new SerializedObject(Object.FindFirstObjectByType<PaxPlaytestSave>());
            string guid = AssetDatabase.AssetPathToGUID(ScenePath);
            if (guid.Length != 32) throw new InvalidOperationException("Palette scene GUID missing.");
            save.FindProperty("saveFile").stringValue = "pax-palette-" + guid + ".xml"; save.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(entry => entry.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets();
            Debug.Log("PAX_PALETTE_AUTHORING_OK " + ScenePath);
        }
        public static void BuildWindows()
        {
            PaxIslandAuthoring.RequireProjectIdentity(); CreatePaletteExpedition();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/PAX-Palette-Windows/PAX.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Directory.CreateDirectory("Logs/PAX");
            File.WriteAllText("Logs/PAX/PaletteBuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Palette Windows build failed.");
            Debug.Log("PAX_PALETTE_BUILD_OK");
        }
    }
}
