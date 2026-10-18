using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxBlueprintAuthoring
    {
        public const string Folder = "Assets/PAX/Blueprints";
        public const string BridgePath = Folder + "/TimberPair.asset", StepsPath = Folder + "/StoneSteps.asset", GhostPath = Folder + "/BlueprintGhost.prefab";
        public const string UiPath = "Assets/PAX/UI/BlueprintFrontier.uxml";
        private static string Arg(string key){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:null;}
        public static string CommonGit(string root)
        {
            string metadata=Path.Combine(Path.GetFullPath(root),".git");
            if(File.Exists(metadata)){string value=File.ReadAllText(metadata).Trim();if(!value.StartsWith("gitdir: ",StringComparison.Ordinal))throw new InvalidOperationException("Invalid worktree metadata.");metadata=Path.GetFullPath(Path.Combine(root,value.Substring(8)));}
            if(!Directory.Exists(metadata))throw new InvalidOperationException("Expected a managed PAX Git repository.");
            string common=Path.Combine(metadata,"commondir");return Path.GetFullPath(File.Exists(common)?Path.Combine(metadata,File.ReadAllText(common).Trim()):metadata).TrimEnd('/','\\');
        }
        public static void ValidateIdentity(string expectedRoot,string expectedCommon)
        {
            PaxIslandAuthoring.ValidateProjectIdentity(expectedRoot);
            string actual=CommonGit(Path.GetDirectoryName(Application.dataPath));
            if(!string.Equals(actual,Path.GetFullPath(expectedCommon).TrimEnd('/','\\'),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("PAX common Git mismatch; no authoring performed.");
            Debug.Log("PAX_BLUEPRINT_COMMON_GIT_CONFIRMED "+actual);
        }
        private static void RequireIdentity()
        {
            string root=Arg("-paxAuthoringProject"),common=Arg("-paxAuthoringCommonGit");
            if(Application.isBatchMode&&(root==null||common==null))throw new InvalidOperationException("Supply independent -paxAuthoringProject and -paxAuthoringCommonGit before authoring.");
            root=root??Directory.GetCurrentDirectory();ValidateIdentity(root,common??CommonGit(root));
        }
        [MenuItem("PAX/Wire construction blueprints on generated frontier")]
        public static void FromMenu(){RequireIdentity();if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EnsureBlueprints();}
        public static void EnsureBlueprints()
        {
            RequireIdentity();if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Author in Edit Mode only.");
            if(!File.Exists(PaxFrontierAuthoring.ScenePath)||!File.Exists(UiPath))throw new FileNotFoundException("Create the saved frontier and authored blueprint UXML first.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/PAX","Blueprints");
            MakeDefinition(BridgePath,"Timber bridge pair",new Vector3Int(0,0,1),"timber");
            MakeDefinition(StepsPath,"Stone ascending pair",new Vector3Int(0,1,1),"stone");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(GhostPath)==null)
            {
                var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name="Blueprint footprint ghost";item.layer=2;item.transform.localScale=Vector3.one*1.01f;Object.DestroyImmediate(item.GetComponent<Collider>());
                item.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Place valid.mat");
                PrefabUtility.SaveAsPrefabAsset(item,GhostPath);Object.DestroyImmediate(item);
            }
            AssetDatabase.SaveAssets();var scene=EditorSceneManager.OpenScene(PaxFrontierAuthoring.ScenePath,OpenSceneMode.Single);
            var owner=Object.FindFirstObjectByType<PaxBlueprintBuilder>();bool changed=false;
            if(owner==null)
            {
                var root=new GameObject("Editable construction blueprints");owner=root.AddComponent<PaxBlueprintBuilder>();var ghosts=new GameObject("Footprint preview instances").transform;ghosts.SetParent(root.transform);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(GhostPath).GetComponent<Renderer>();
                var builder=Object.FindFirstObjectByType<PaxBuilder>();var builderData=new SerializedObject(builder);
                owner.Configure(builder,Object.FindFirstObjectByType<PaxPause>(),Object.FindFirstObjectByType<PaxPlaytestSave>(),Object.FindFirstObjectByType<UIDocument>(),new[]{AssetDatabase.LoadAssetAtPath<PaxConstructionBlueprint>(BridgePath),AssetDatabase.LoadAssetAtPath<PaxConstructionBlueprint>(StepsPath)},prefab,ghosts,(Material)builderData.FindProperty("validMaterial").objectReferenceValue,(Material)builderData.FindProperty("invalidMaterial").objectReferenceValue);changed=true;
            }
            var ui=Object.FindFirstObjectByType<UIDocument>();var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);if(ui.visualTreeAsset!=tree){ui.visualTreeAsset=tree;changed=true;}
            var help=new SerializedObject(Object.FindFirstObjectByType<PaxPause>());
            string instructions="House: gather 3 Timber. Cave: gather 2 Stone. Read both stations. B previews a 2-cell Timber bridge; N previews 2 ascending Stone steps. T rotates 90 degrees. Release held controls, then RMB places the entire footprint only if every cell and the full cost are valid. Backspace or 1–5 returns to single-cell. Build the remaining bridge cell singly, jump the steps and F connects the relay. Mining refunds actual materials. F5/F9/F8 keep typed tiles, stocks and harvest history; F6 resets only notes. R returns to camp.";
            if(help.FindProperty("missionHelp").stringValue!=instructions){help.FindProperty("missionHelp").stringValue=instructions;help.ApplyModifiedPropertiesWithoutUndo();changed=true;}
            var pile=new SerializedObject(Object.FindFirstObjectByType<PaxStockpile>());string brief="House: 3 Timber / Cave: 2 Stone. B bridge pair · N ascending steps · T rotate · Backspace or 1–5 single-cell. Release, then RMB places all or nothing. Esc: full controls/route.";
            if(pile.FindProperty("instructions").stringValue!=brief){pile.FindProperty("instructions").stringValue=brief;pile.ApplyModifiedPropertiesWithoutUndo();changed=true;}
            if(changed){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}AssetDatabase.SaveAssets();Debug.Log("PAX_BLUEPRINT_AUTHORING_OK frontier only; world geometry/slot unchanged");
        }
        private static void MakeDefinition(string path,string title,Vector3Int second,string id)
        {
            if(AssetDatabase.LoadAssetAtPath<PaxConstructionBlueprint>(path)!=null)return;
            var definition=ScriptableObject.CreateInstance<PaxConstructionBlueprint>();definition.Configure(title,new[]{new PaxConstructionBlueprint.Cell{offset=Vector3Int.zero,materialId=id},new PaxConstructionBlueprint.Cell{offset=second,materialId=id}});AssetDatabase.CreateAsset(definition,path);
        }
        public static void BuildWindows()
        {
            RequireIdentity();EnsureBlueprints();var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{PaxFrontierAuthoring.ScenePath},locationPathName="Builds/PAX-Blueprints-Windows/PAX.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Directory.CreateDirectory("Logs/PAX");File.WriteAllText("Logs/PAX/BlueprintBuildSummary.txt",$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Blueprint Windows build failed.");Debug.Log("PAX_BLUEPRINT_BUILD_OK");
        }
    }
}
