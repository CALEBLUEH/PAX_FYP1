using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pax.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxBlueprintTests
    {
        [Test] public void CardinalRotationPreservesMaterialsAndInvalidFootprintsAreRejected()
        {
            var d=ScriptableObject.CreateInstance<PaxConstructionBlueprint>();
            try
            {
                d.Configure("Mixed",new[]{new PaxConstructionBlueprint.Cell{offset=Vector3Int.zero,materialId="timber"},new PaxConstructionBlueprint.Cell{offset=new Vector3Int(0,1,1),materialId="stone"}});
                var anchor=new Vector3Int(3,0,3);var expected=new[]{new Vector3Int(0,1,1),new Vector3Int(1,1,0),new Vector3Int(0,1,-1),new Vector3Int(-1,1,0)};
                for(int t=0;t<4;t++){Assert.IsTrue(d.TryPlan(anchor,t,out var p,out _));Assert.AreEqual(anchor,p[0].Cell);Assert.AreEqual(anchor+expected[t],p[1].Cell);Assert.AreEqual("stone",p[1].MaterialId);}
                foreach(var values in new[]{new[]{new PaxConstructionBlueprint.Cell{offset=Vector3Int.zero,materialId="timber"},new PaxConstructionBlueprint.Cell{offset=Vector3Int.zero,materialId="stone"}},new[]{new PaxConstructionBlueprint.Cell{offset=Vector3Int.zero,materialId="timber"},new PaxConstructionBlueprint.Cell{offset=Vector3Int.forward,materialId="bad id!"}},new[]{new PaxConstructionBlueprint.Cell{offset=Vector3Int.zero,materialId="timber"},new PaxConstructionBlueprint.Cell{offset=new Vector3Int(int.MinValue,0,0),materialId="timber"}}})
                {d.Configure("Bad",values);Assert.IsFalse(d.TryPlan(anchor,0,out _,out _));}
                d.Configure("Too many",Enumerable.Range(0,9).Select(i=>new PaxConstructionBlueprint.Cell{offset=new Vector3Int(i,0,0),materialId="timber"}).ToArray());Assert.IsFalse(d.TryPlan(anchor,0,out _,out _));
            }
            finally{Object.DestroyImmediate(d);}
        }
        [Test] public void WrongProjectAndCommonGitAreRejectedBeforeAuthoring()
        {
            string root=Path.GetDirectoryName(Application.dataPath),common=PaxBlueprintAuthoring.CommonGit(root);
            Assert.Throws<InvalidOperationException>(()=>PaxBlueprintAuthoring.ValidateIdentity(Path.GetTempPath(),common));
            Assert.Throws<InvalidOperationException>(()=>PaxBlueprintAuthoring.ValidateIdentity(root,Path.GetTempPath()));PaxBlueprintAuthoring.ValidateIdentity(root,common);
        }
        [Test] public void FrontierHasExactlyTwoAuthoredAssetsSafeGhostAndUnusedEditableBindings()
        {
            EditorSceneManager.OpenScene(PaxFrontierAuthoring.ScenePath,OpenSceneMode.Single);var b=Object.FindFirstObjectByType<PaxBlueprintBuilder>();Assert.NotNull(b);Assert.AreEqual(2,b.Definitions.Count);
            var bridge=AssetDatabase.LoadAssetAtPath<PaxConstructionBlueprint>(PaxBlueprintAuthoring.BridgePath);var steps=AssetDatabase.LoadAssetAtPath<PaxConstructionBlueprint>(PaxBlueprintAuthoring.StepsPath);
            Assert.AreSame(bridge,b.Definitions[0]);Assert.AreSame(steps,b.Definitions[1]);Assert.IsTrue(bridge.TryPlan(Vector3Int.zero,0,out var p,out _));Assert.AreEqual(2,p.Length);Assert.IsTrue(p.All(t=>t.MaterialId=="timber"));
            Assert.IsTrue(steps.TryPlan(Vector3Int.zero,0,out p,out _));Assert.AreEqual(new Vector3Int(0,1,1),p[1].Cell);Assert.IsTrue(p.All(t=>t.MaterialId=="stone"));
            var data=new SerializedObject(b);foreach(string field in new[]{"builder","pause","storage","document","ghostPrefab","ghostRoot","validMaterial","invalidMaterial"})Assert.NotNull(data.FindProperty(field).objectReferenceValue,field);
            Assert.AreEqual((int)KeyCode.B,data.FindProperty("bridgeKey").intValue);Assert.AreEqual((int)KeyCode.N,data.FindProperty("stepsKey").intValue);Assert.AreEqual((int)KeyCode.T,data.FindProperty("rotateKey").intValue);Assert.AreEqual((int)KeyCode.Backspace,data.FindProperty("cancelKey").intValue);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PaxBlueprintAuthoring.GhostPath);Assert.AreEqual(0,prefab.GetComponentsInChildren<Collider>(true).Length);Assert.NotNull(prefab.GetComponent<Renderer>().sharedMaterial);Assert.AreEqual(2,prefab.layer);
            Assert.NotNull(Object.FindFirstObjectByType<UIDocument>().visualTreeAsset.CloneTree().Q<Label>("construction-blueprint"));
        }
        [Test] public void TargetAuthoringIsByteIdempotentAndPriorScenesAndAssetsAreUnchanged()
        {
            string[] paths=Directory.GetFiles("Assets/PAX/Scenes","*.unity").Concat(new[]{PaxBlueprintAuthoring.BridgePath,PaxBlueprintAuthoring.StepsPath,PaxBlueprintAuthoring.GhostPath,PaxBlueprintAuthoring.UiPath,"ProjectSettings/EditorBuildSettings.asset"}).ToArray();var before=paths.Select(File.ReadAllBytes).ToArray();PaxBlueprintAuthoring.EnsureBlueprints();PaxBlueprintAuthoring.EnsureBlueprints();for(int i=0;i<paths.Length;i++)CollectionAssert.AreEqual(before[i],File.ReadAllBytes(paths[i]),paths[i]);
        }
    }
}
