using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxChartAuthoring
    {
        public const string ScenePath="Assets/PAX/Scenes/PaxLamplighterChart.unity";
        public const string UiPath="Assets/PAX/UI/LamplighterChart.uxml";
        public const string RouteGuide="Amber = Timber (2); cyan = Stone (3). Bridge: Ctrl precise + Q/E to view the last tile's forward side face, then RMB it. Home resets the view for the Stone stairs. Mine wrong tiles to refund. R rescues a fall.";
        [MenuItem("PAX/Create Lamplighter chart expedition if missing")]
        public static void CreateFromMenu(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())CreateChart();}
        public static void CreateChart()
        {
            PaxIslandAuthoring.RequireProjectIdentity();
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Author in Edit Mode only.");
            if(File.Exists(ScenePath)){Debug.Log("Lamplighter chart exists; preserving scene and saves.");return;}
            var scene=EditorSceneManager.OpenScene(PaxResourceAuthoring.ScenePath,OpenSceneMode.Single);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Cannot create chart scene.");
            var world=Object.FindFirstObjectByType<PaxWorld>();var player=Object.FindFirstObjectByType<PaxPlayer>();var rig=Object.FindFirstObjectByType<PaxCamera>();
            var mission=Object.FindFirstObjectByType<PaxExpedition>();var cache=Object.FindFirstObjectByType<PaxResourceCache>();var stock=Object.FindFirstObjectByType<PaxStockpile>();var ui=Object.FindFirstObjectByType<UIDocument>();
            var data=new SerializedObject(world);var root=(Transform)data.FindProperty("blockRoot").objectReferenceValue;var prefab=(PaxBlock)data.FindProperty("blockPrefab").objectReferenceValue;
            foreach(Transform child in root.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            foreach(Transform child in world.transform.Cast<Transform>().Where(t=>t!=root).ToArray())Object.DestroyImmediate(child.gameObject);
            foreach(Transform child in mission.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
            var oldMarkers=new SerializedObject(cache).FindProperty("gatherMarkers");
            for(int i=0;i<oldMarkers.arraySize;i++)Object.DestroyImmediate(((Transform)oldMarkers.GetArrayElementAtIndex(i).objectReferenceValue).gameObject);
            foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))Object.DestroyImmediate(text.gameObject);
            Object.DestroyImmediate(world.GetComponent<PaxGeneratedIsland>());
            var grass=AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Grass.mat");var earth=AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Earth.mat");var rock=AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Rock.mat");var timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Timber.mat");
            var blocks=new Dictionary<Vector3Int,PaxBlock>();
            void Tile(int x,int y,int z,Material material,bool mineable=true)
            {
                var cell=new Vector3Int(x,y,z);if(blocks.ContainsKey(cell))return;
                var tile=((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,root)).GetComponent<PaxBlock>();tile.name="Authored "+cell;tile.Configure(cell,tile.GetComponent<Renderer>(),tile.GetComponent<Collider>(),mineable);tile.GetComponent<Renderer>().sharedMaterial=material;blocks.Add(cell,tile);
            }
            for(int x=-10;x<=9;x++)for(int z=-3;z<=3;z++)
            {
                if(x>=-3&&x<=1){Tile(x,-4,z,rock,false);continue;}
                Tile(x,-3,z,earth,false);Tile(x,-2,z,earth);Tile(x,-1,z,grass);
            }
            for(int x=6;x<=8;x++)for(int z=-1;z<=1;z++){Tile(x,0,z,rock,false);Tile(x,1,z,rock,false);}
            // A narrow lookout canopy gives camera/cutaway context without blocking access.
            for(int x=6;x<=8;x++){Tile(x,5,-1,timber,false);Tile(x,5,0,timber,false);Tile(x,5,1,timber,false);}
            for(int y=2;y<5;y++){Tile(8,y,-1,timber,false);Tile(8,y,1,timber,false);}
            var gather=new List<Transform>();
            foreach(var cell in Enumerable.Range(-8,5).Select(x=>new Vector3Int(x,0,-2)).Concat(Enumerable.Range(-8,3).Select(x=>new Vector3Int(x,0,2))))
            {
                Tile(cell.x,cell.y,cell.z,cell.z==-2?timber:rock);
                var mark=Marker("Gather "+(cell.z==-2?"Timber ":"Stone ")+cell,cache.transform,PaxGrid.Centre(cell));gather.Add(mark);
            }
            cache.Configure(gather.ToArray());world.Configure(prefab,root,blocks.Values.ToList());
            data.Update();var minimum=data.FindProperty("minimum").vector3IntValue;minimum.y=-4;data.FindProperty("minimum").vector3IntValue=minimum;data.ApplyModifiedPropertiesWithoutUndo();
            var spawn=(Transform)new SerializedObject(player).FindProperty("respawn").objectReferenceValue;spawn.position=new Vector3(-8,0.03f,0);player.transform.position=spawn.position;
            rig.SnapToTarget();
            var amber=NewMaterial("Chart amber blueprint",Shader.Find("PAX/Preview"),new Color(1,0.65f,0.2f,0.3f));var cyan=AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition blueprint.mat");var markerMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition marker.mat");
            PaxRepairMarker Repair(Vector3Int cell,string id,Material mat)
            {
                var visual=Visual(id+" repair "+cell,PrimitiveType.Cube,mission.transform,PaxGrid.Centre(cell),Vector3.one*0.99f,mat);
                var repair=visual.gameObject.AddComponent<PaxRepairMarker>();repair.Configure(world,visual);repair.RequireMaterial(id);return repair;
            }
            var bridge=Enumerable.Range(-3,5).Select(x=>Repair(new Vector3Int(x,-1,0),"timber",amber)).ToArray();
            var steps=new[]{new Vector3Int(5,0,0),new Vector3Int(5,1,0),new Vector3Int(4,0,0)}.Select(c=>Repair(c,"stone",cyan)).ToArray();
            PaxSurveySite Site(string title,string note,Vector3 position)
            {
                var visual=Visual(title+" discovery",PrimitiveType.Cylinder,mission.transform,position,new Vector3(0.7f,0.025f,0.7f),markerMaterial);
                var site=visual.gameObject.AddComponent<PaxSurveySite>();site.Configure(title,note,visual);return site;
            }
            var departure=Site("Departure log","Timber for the crossing; Stone for the lookout.",new Vector3(-6,0.03f,1));
            var mooring=Site("Old mooring","The lamplighter kept the inland chart above the tide.",new Vector3(2,0.03f,1));
            var chart=Site("Lamplighter's chart","Three lamps mark the inland ridge: Ember Reach.",new Vector3(7,2.08f,0));
            var lamp=Visual("Recovered chart signal",PrimitiveType.Sphere,mission.transform,new Vector3(7,3.1f,0.8f),Vector3.one*0.3f,AssetDatabase.LoadAssetAtPath<Material>("Assets/PAX/Materials/Expedition beacon.mat"));
            var parchment=NewMaterial("Lamplighter parchment",Shader.Find("Standard"),new Color(0.9f,0.83f,0.55f));
            Visual("Authored chart board",PrimitiveType.Cube,mission.transform,new Vector3(7,2.04f,0),new Vector3(1.1f,0.05f,0.8f),parchment);
            for(int i=0;i<3;i++)Visual("Chart inland route mark "+i,PrimitiveType.Cube,mission.transform,new Vector3(6.65f+i*0.3f,2.09f,-0.2f+i*0.15f),new Vector3(0.12f,0.04f,0.12f),markerMaterial);
            mission.Configure(player,new[]{departure,mooring,chart},bridge.Concat(steps).ToArray(),chart.transform,lamp);
            var missionData=new SerializedObject(mission);
            var copy=new Dictionary<string,string>{{"expeditionTitle","Lowtide / Lamplighter's Chart"},{"introduction","A broken approach hides the lamplighter's last chart. Restore the route and discover where the inland lamps lead."},{"surveyObjective","Read the departure log, cross the Timber approach to the old mooring, then climb the Stone lookout to the chart."},{"repairObjective","Repairs require exact materials: 5 Timber approach cells and 3 Stone stair cells. Mine wrong tiles to recover their units."},{"returnObjective","Stand on the lookout's chart mark and press F to recover the inland route."},{"completeObjective","Chart recovered: three lamps mark the ridge to Ember Reach. Keep building or F6 replay; resources and repairs stay."},{"missingSurveyMessage","Discover the departure log, old mooring and lookout chart first."},{"missingRepairMessage","Restore all five Timber crossing cells and three Stone stair cells with their matching materials."},{"returnMessage","Reach the lookout chart mark on foot, then press F."},{"successMessage","The inland route has a name: Ember Reach. Chart recovered."},{"interruptedMessage","The route is damaged or has a wrong material. Restore it, then recover the chart again."},{"alreadyActiveMessage","The chart is already recovered. Keep exploring or F6 replay."}};
            foreach(var pair in copy)missionData.FindProperty(pair.Key).stringValue=pair.Value;missionData.ApplyModifiedPropertiesWithoutUndo();
            if(!File.Exists(UiPath))
            {
                string markup=File.ReadAllText(PaxResourceAuthoring.UiPath).Replace("Cyan = platform blueprint","Amber = Timber repair / Cyan = Stone repair").Replace("F Activate beacon   ·   F6 Restart survey","F Recover chart   ·   F6 Restart discoveries");
                markup=markup.Replace("<ui:Label name=\"expedition-repairs\"","<ui:Label name=\"chart-route\" text=\"5 Timber approach / 3 Stone lookout\" style=\"font-size: 13px; color: rgb(255, 219, 151); white-space: normal; margin-top: 8px;\" picking-mode=\"Ignore\" />\n        <ui:Label name=\"expedition-repairs\"");
                File.WriteAllText(UiPath,markup);AssetDatabase.ImportAsset(UiPath);
            }
            ui.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);
            var route=mission.gameObject.AddComponent<PaxChartRoute>();route.Configure(ui,bridge,steps,new[]{departure.transform,Marker("Near bank",mission.transform,new Vector3(-4,0.03f,0)),mooring.transform,Marker("Stair takeoff",mission.transform,new Vector3(3,0.03f,0)),Marker("First stair landing",mission.transform,new Vector3(4,1.03f,0)),Marker("Lookout landing",mission.transform,new Vector3(6,2.03f,0)),chart.transform});
            var routeData=new SerializedObject(route);routeData.FindProperty("guide").stringValue=RouteGuide;routeData.ApplyModifiedPropertiesWithoutUndo();
            var stockData=new SerializedObject(stock);stockData.FindProperty("instructions").stringValue="Mine camp caches: 5 Timber (south row) + 3 Stone (north row). 2 selects Timber; 3 selects Stone. Bridge outward from the near bank. R returns to camp after a fall.";stockData.ApplyModifiedPropertiesWithoutUndo();
            var hudData=new SerializedObject(Object.FindFirstObjectByType<PaxExpeditionHud>());hudData.FindProperty("progressFormat").stringValue="Discoveries {0}/{1}  ·  Matching repairs {2}/{3}";hudData.ApplyModifiedPropertiesWithoutUndo();
            string guid=AssetDatabase.AssetPathToGUID(ScenePath);if(guid.Length!=32)throw new InvalidOperationException("Chart slot GUID missing.");
            var storage=new SerializedObject(Object.FindFirstObjectByType<PaxPlaytestSave>());storage.FindProperty("saveFile").stringValue="pax-chart-"+guid+".xml";storage.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(e=>e.path==ScenePath))scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
            Debug.Log("PAX_CHART_AUTHORING_OK 5 Timber / 3 Stone repairs, far lookout discovery, "+blocks.Count+" tiles");
        }
        private static Transform Marker(string name,Transform parent,Vector3 position){var obj=new GameObject(name).transform;obj.SetParent(parent);obj.position=position;return obj;}
        private static Renderer Visual(string name,PrimitiveType shape,Transform parent,Vector3 position,Vector3 scale,Material material)
        {var obj=GameObject.CreatePrimitive(shape);obj.name=name;obj.layer=2;obj.transform.SetParent(parent);obj.transform.position=position;obj.transform.localScale=scale;Object.DestroyImmediate(obj.GetComponent<Collider>());var r=obj.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return r;}
        private static Material NewMaterial(string name,Shader shader,Color color){string path="Assets/PAX/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;material=new Material(shader){name=name,color=color};AssetDatabase.CreateAsset(material,path);return material;}
        public static void BuildWindows()
        {PaxIslandAuthoring.RequireProjectIdentity();CreateChart();EnsureRouteGuide();var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/PAX-Chart-Windows/PAX.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});Directory.CreateDirectory("Logs/PAX");File.WriteAllText("Logs/PAX/ChartBuildSummary.txt",$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Chart build failed.");Debug.Log("PAX_CHART_BUILD_OK");}
        public static void EnsureRouteGuide()
        {PaxIslandAuthoring.RequireProjectIdentity();string before=File.ReadAllText(UiPath),after=before.Replace("F Activate beacon   ·   F6 Restart survey","F Recover chart   ·   F6 Restart discoveries");if(before!=after){File.WriteAllText(UiPath,after);AssetDatabase.ImportAsset(UiPath);}var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var route=new SerializedObject(Object.FindFirstObjectByType<PaxChartRoute>());string text=route.FindProperty("guide").stringValue;if(text=="Amber outlines require Timber (2); cyan outlines require Stone (3). Wrong material? Mine it back and replace it. If you fall, R returns to camp."||text=="Amber = Timber (2); cyan = Stone (3). For bridge side faces use Ctrl precise + Q/E to view forward from the bank, then RMB the last solid tile's forward face. Mine wrong tiles to refund. R rescues a fall."){route.FindProperty("guide").stringValue=RouteGuide;route.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}}
    }
}
