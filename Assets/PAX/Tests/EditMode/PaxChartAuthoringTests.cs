using System.IO;
using System.Linq;
using NUnit.Framework;
using Pax.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxChartAuthoringTests
    {
        [Test] public void ChartSceneIsAuthoredWiredBoundedAndIdempotent()
        {
            string[] paths={PaxChartAuthoring.ScenePath,PaxChartAuthoring.UiPath,PaxResourceAuthoring.ScenePath,PaxResourceAuthoring.UiPath,PaxPaletteAuthoring.ScenePath,PaxPaletteAuthoring.UiPath,PaxExpeditionAuthoring.ScenePath,"Assets/PAX/UI/Expedition.uxml",PaxIslandAuthoring.ScenePath,PaxPrototypeAuthoring.ScenePath,"ProjectSettings/EditorBuildSettings.asset"};
            var before=paths.Select(File.ReadAllBytes).ToArray();PaxChartAuthoring.CreateChart();PaxChartAuthoring.EnsureRouteGuide();for(int i=0;i<paths.Length;i++)CollectionAssert.AreEqual(before[i],File.ReadAllBytes(paths[i]));
            var scene=EditorSceneManager.OpenScene(PaxChartAuthoring.ScenePath,OpenSceneMode.Single);var world=Object.FindFirstObjectByType<PaxWorld>();world.Reindex();
            var route=Object.FindFirstObjectByType<PaxChartRoute>();Assert.AreEqual(5,route.Timber.Length);Assert.AreEqual(3,route.Stone.Length);
            Assert.IsTrue(route.Timber.All(r=>r.RequiredMaterialId=="timber"));Assert.IsTrue(route.Stone.All(r=>r.RequiredMaterialId=="stone"));
            foreach(var block in world.Blocks){Assert.IsTrue(world.InBounds(block.Cell));Assert.NotNull(block.Solid);Assert.NotNull(block.SolidShadow);}
            var nodes=Object.FindFirstObjectByType<PaxResourceCache>().Cells;Assert.AreEqual(8,nodes.Length);foreach(var cell in nodes)Assert.IsTrue(world.Contains(cell));
            Assert.That(Path.GetFileName(Object.FindFirstObjectByType<PaxPlaytestSave>().SavePath),Does.StartWith("pax-chart-"));
            var tree=Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree();Assert.NotNull(tree.Q<Label>("chart-route"));
            Assert.IsTrue(tree.Query<Label>().ToList().Any(label=>label.text=="F Recover chart   ·   F6 Restart discoveries"));
            Assert.IsFalse(tree.Query<Label>().ToList().Any(label=>label.text.Contains("F Activate beacon")));
            foreach(var root in scene.GetRootGameObjects())foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {Assert.NotNull(component);if(!component.GetType().Namespace.StartsWith("Pax"))continue;var property=new SerializedObject(component).GetIterator();while(property.NextVisible(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.name!="m_Script")Assert.NotNull(property.objectReferenceValue,component.GetType().Name+"."+property.propertyPath);}
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==PaxChartAuthoring.ScenePath));
        }
    }
}
