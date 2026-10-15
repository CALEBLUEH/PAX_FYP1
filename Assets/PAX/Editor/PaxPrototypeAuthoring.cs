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
using Object = UnityEngine.Object;

namespace Pax.Editor
{
    public static class PaxPrototypeAuthoring
    {
        public const string ScenePath = "Assets/PAX/Scenes/PaxPrototype.unity";
        private const string Root = "Assets/PAX";

        [MenuItem("PAX/Create prototype if missing")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreatePrototype();
        }

        public static void CreatePrototype()
        {
            // Idempotent: rerunning never rebuilds or overwrites an authored scene.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("PAX scene already exists. Preserving authored assets.");
                return;
            }
            foreach (string directory in new[] { "Materials", "Prefabs", "Scenes", "UI" }) EnsureFolder(Root + "/" + directory);
            // Create the scene before loading assets: NewScene can unload stack-only asset references.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var blockShader = Shader.Find("PAX/Block Cutaway");
            var previewShader = Shader.Find("PAX/Preview");
            if (blockShader == null || previewShader == null) throw new InvalidOperationException("PAX shaders were not imported.");
            var grass = Material("Grass", blockShader, new Color(0.3f, 0.55f, 0.38f));
            var earth = Material("Earth", blockShader, new Color(0.4f, 0.31f, 0.23f));
            var timber = Material("Timber", blockShader, new Color(0.68f, 0.46f, 0.25f));
            var roof = Material("Roof", blockShader, new Color(0.37f, 0.48f, 0.64f));
            var rock = Material("Rock", blockShader, new Color(0.48f, 0.5f, 0.57f));
            var character = Material("Player", Shader.Find("Standard"), new Color(1, 0.78f, 0.23f));
            var mine = Material("Mine preview", previewShader, new Color(1, 0.8f, 0.1f, 0.25f));
            var valid = Material("Place valid", previewShader, new Color(0.2f, 1, 0.5f, 0.3f));
            var invalid = Material("Place blocked", previewShader, new Color(1, 0.2f, 0.2f, 0.3f));
            var blockPrefab = CreateBlockPrefab(grass);
            EnsureSolidShadows();
            var world = new GameObject("Editable tile world").AddComponent<PaxWorld>();
            var blockRoot = new GameObject("Authored tiles").transform;
            blockRoot.SetParent(world.transform);
            var tiles = new Dictionary<Vector3Int, PaxBlock>();
            Action<int,int,int,Material,bool> tile = (x,y,z,material,mineable) =>
            {
                var cell = new Vector3Int(x,y,z);
                if (tiles.ContainsKey(cell)) return;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab.gameObject, blockRoot);
                var block = instance.GetComponent<PaxBlock>();
                block.name = $"Tile {x},{y},{z}";
                block.Configure(cell, block.GetComponent<Renderer>(), block.GetComponent<Collider>(), mineable);
                block.GetComponent<Renderer>().sharedMaterial = material;
                tiles.Add(cell,block);
            };
            for (int x = -12; x <= 12; x++)
            for (int z = -11; z <= 11; z++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(z) > 20) continue;
                if (x >= 0 && x <= 1 && z >= 6 && z <= 7) continue;
                tile(x,-2,z,earth,false); // Protected foundation preserves a repeatable test island.
                tile(x,-1,z,grass,true);
            }
            // House: walk through a two-tile entrance on the south side.
            for (int x = -9; x <= -3; x++)
            for (int z = 3; z <= 9; z++)
            {
                bool boundary = x == -9 || x == -3 || z == 3 || z == 9;
                bool door = z == 3 && (x == -6 || x == -5);
                if (boundary) for (int y = 0; y < 3; y++) if (!door || y == 2) tile(x,y,z,timber,true);
                tile(x,3,z,roof,true);
            }
            // Cave: thicker rock roof and a wide south-facing opening.
            for (int x = 4; x <= 10; x++)
            for (int z = 3; z <= 10; z++)
            {
                bool boundary = x == 4 || x == 10 || z == 10 || (z == 3 && x != 7 && x != 8);
                if (boundary) for (int y = 0; y < 4; y++) tile(x,y,z,rock,true);
                if (z == 3 && (x == 7 || x == 8)) for (int y = 2; y < 4; y++) tile(x,y,z,rock,true);
                tile(x,4,z,rock,true); tile(x,5,z,grass,true);
            }
            // Enclosed low room with a two-high door, separate from the cave.
            for (int x = 4; x <= 8; x++)
            for (int z = -9; z <= -5; z++)
            {
                bool boundary = x == 4 || x == 8 || z == -9 || z == -5;
                bool door = (z == -5 && x == 6) || (x == 4 && z == -7);
                if (boundary && !door) for (int y = 0; y < 3; y++) tile(x,y,z,timber,true);
                tile(x,3,z,roof,true);
            }
            // High hall connects through a one-tile passage to the low room.
            for (int x = -3; x <= 2; x++)
            for (int z = -10; z <= -5; z++)
            {
                bool boundary = x == -3 || x == 2 || z == -10 || z == -5;
                bool door = (z == -5 && (x == -1 || x == 0)) || (x == 2 && z == -7);
                if (boundary) for (int y = 0; y < 5; y++) if (!door || y >= 2) tile(x,y,z,timber,true);
                tile(x,5,z,roof,true);
            }
            for (int y = 0; y < 3; y++) { tile(3,y,-8,timber,true); tile(3,y,-6,timber,true); }
            tile(3,3,-7,roof,true);
            // Two-by-two shaft with a three-tile drop and a jumpable exit step.
            for (int x = -1; x <= 2; x++)
            for (int z = 5; z <= 8; z++)
            {
                tile(x,-4,z,rock,false);
                if (x == -1 || x == 2 || z == 5 || z == 8) tile(x,-3,z,rock,true);
            }
            tile(1,-3,7,rock,true); tile(1,-2,6,rock,true);
            // Two one-tile rises, a two-tile ledge, and an isolated mining target.
            for (int x = -9; x <= -7; x++)
            for (int z = -6; z <= -2; z++)
            for (int y = 0; y <= Mathf.Min(2, z + 6); y++) tile(x,y,z,rock,true);
            tile(-3,0,-3,rock,true); tile(-3,1,-3,rock,true);
            world.Configure(blockPrefab, blockRoot, tiles.Values.ToList());

            var spawn = new GameObject("Spawn marker").transform;
            spawn.position = new Vector3(0,0.03f,-3);
            var playerObject = new GameObject("Player");
            playerObject.layer = 2; // Ignore camera/build rays; CharacterController still collides with the world.
            playerObject.transform.position = spawn.position;
            var body = playerObject.AddComponent<CharacterController>();
            // A nominal two-tile body needs contact clearance in a two-tile voxel doorway.
            body.height = 1.96f; body.radius = 0.45f; body.center = Vector3.up * 0.98f;
            body.skinWidth = 0.015f; body.stepOffset = 0.05f; body.slopeLimit = 45; body.minMoveDistance = 0;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Editable body visual"; visual.layer = 2;
            visual.transform.SetParent(playerObject.transform,false);
            visual.transform.localPosition = Vector3.up;
            visual.transform.localScale = new Vector3(0.85f,1,0.85f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = character;
            var player = playerObject.AddComponent<PaxPlayer>();

            var cameraObject = new GameObject("Prototype camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 10;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 120;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.065f,0.11f,0.17f);
            cameraObject.AddComponent<AudioListener>();
            var rig = cameraObject.AddComponent<PaxCamera>();
            rig.Configure(player.transform,camera); rig.SnapToTarget();
            player.Configure(body,camera.transform,spawn);
            var previews = new GameObject("Authored interaction previews").transform;
            var minePreview = Preview("Mining selection",previews,mine);
            var placePreview = Preview("Placement hologram",previews,valid);
            var builder = new GameObject("Tile interaction").AddComponent<PaxBuilder>();
            builder.Configure(world,player,camera,minePreview,placePreview,placePreview.GetComponent<Renderer>(),valid,invalid);
            var input = playerObject.AddComponent<PaxInput>();
            input.Configure(player,rig,builder,spawn);
            var cutaway = new GameObject("Contextual cutaway").AddComponent<PaxCutaway>();
            cutaway.Configure(world,player,camera);

            var sun = new GameObject("World sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f,0.39f,0.46f);
            RenderSettings.sun = sun;
            world.gameObject.AddComponent<PaxWorldLighting>().Configure(world,sun);
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600,900);
            AssetDatabase.CreateAsset(panel,Root + "/UI/PrototypePanel.asset");
            var hudObject = new GameObject("Authored HUD");
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/Prototype.uxml");
            hudObject.AddComponent<PaxHud>().Configure(document,player,builder,cutaway);
            Label("OPEN FIELD",new Vector3(-1,0.04f,-7),camera.transform.rotation);
            Label("HOUSE",new Vector3(-6,0.04f,1),camera.transform.rotation);
            Label("CAVE",new Vector3(7,0.04f,1),camera.transform.rotation);
            Label("LOW ROOM",new Vector3(6,0.04f,-3.5f),camera.transform.rotation);
            Label("JUMP STEPS",new Vector3(-8,0.04f,-8),camera.transform.rotation);
            Label("HIGH HALL",new Vector3(0,0.04f,-4),camera.transform.rotation);
            Label("SHAFT",new Vector3(0,0.04f,4.5f),camera.transform.rotation);

            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            // Preserve existing scenes and unrelated config objects such as the Unity AI UI reference.
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"PAX_AUTHORING_OK: {tiles.Count} authored tiles, saved {ScenePath}");
        }

        private static PaxBlock CreateBlockPrefab(Material material)
        {
            string path = Root + "/Prefabs/Tile.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<PaxBlock>(path);
            if (existing != null) return existing;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Tile";
            cube.GetComponent<Renderer>().sharedMaterial = material;
            cube.AddComponent<PaxBlock>().Configure(Vector3Int.zero,cube.GetComponent<Renderer>(),cube.GetComponent<Collider>());
            var saved = PrefabUtility.SaveAsPrefabAsset(cube,path).GetComponent<PaxBlock>();
            Object.DestroyImmediate(cube);
            return saved;
        }
        private static Material Material(string name, Shader shader, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material,path);
            return material;
        }
        private static Transform Preview(string name, Transform parent, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.layer = 2; cube.transform.SetParent(parent);
            cube.transform.localScale = Vector3.one * 1.015f;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            cube.SetActive(false);
            return cube.transform;
        }
        private static void Label(string text, Vector3 position, Quaternion rotation)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.text = text; label.fontSize = 48; label.characterSize = 0.045f;
            label.anchor = TextAnchor.MiddleCenter; label.color = new Color(0.84f,0.91f,0.85f);
            label.transform.SetPositionAndRotation(position,rotation);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }

        public static void BuildWindows()
        {
            CreatePrototype();
            EnsureSolidShadows();
            EnsureWorldLighting();
            EnsureCaveDoorLintel();
            EnsurePlayerClearance();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/PAX-Prototype-Windows/PAX.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Directory.CreateDirectory("Logs/PAX");
            File.WriteAllText("Logs/PAX/BuildSummary.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("PAX Windows build failed");
            Debug.Log("PAX_WINDOWS_BUILD_OK");
        }

        public static void EnsureSolidShadows()
        {
            string path = Root + "/Prefabs/Tile.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var block = contents.GetComponent<PaxBlock>();
                if (block.SolidShadow != null) return;
                var shadowObject = new GameObject("Independent solid shadow");
                shadowObject.transform.SetParent(contents.transform,false);
                shadowObject.AddComponent<MeshFilter>().sharedMesh=contents.GetComponent<MeshFilter>().sharedMesh;
                var caster=shadowObject.AddComponent<MeshRenderer>();
                caster.sharedMaterial=Material("Solid world shadow",Shader.Find("Standard"),Color.gray);
                caster.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                caster.receiveShadows=false;
                contents.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                block.Configure(block.Cell,contents.GetComponent<Renderer>(),contents.GetComponent<Collider>(),block.Mineable,caster);
                PrefabUtility.SaveAsPrefabAsset(contents,path);
                AssetDatabase.SaveAssets();
                Debug.Log("PAX_SOLID_SHADOW_AUTHORING_OK");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static void EnsureWorldLighting()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var world=Object.FindFirstObjectByType<PaxWorld>();
            if(world.GetComponent<PaxWorldLighting>()!=null) return;
            world.gameObject.AddComponent<PaxWorldLighting>().Configure(world,RenderSettings.sun);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PAX_PHYSICAL_LIGHTING_AUTHORING_OK");
        }

        public static void EnsureCaveDoorLintel()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var world=Object.FindFirstObjectByType<PaxWorld>();
            world.Reindex();
            var data=new SerializedObject(world);
            var prefab=(PaxBlock)data.FindProperty("blockPrefab").objectReferenceValue;
            var parent=(Transform)data.FindProperty("blockRoot").objectReferenceValue;
            var blocks=world.Blocks.ToList();
            bool changed=false;
            for(int x=7;x<=8;x++)
            for(int y=2;y<=3;y++)
            {
                var cell=new Vector3Int(x,y,3);
                if(world.Contains(cell)) continue;
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,parent);
                instance.name=$"Tile {x},{y},3";
                var block=instance.GetComponent<PaxBlock>();
                block.Configure(cell,instance.GetComponent<Renderer>(),instance.GetComponent<Collider>());
                instance.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Rock.mat");
                blocks.Add(block); changed=true;
            }
            if(!changed) return;
            world.Configure(prefab,parent,blocks);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PAX_TWO_HIGH_CAVE_DOOR_AUTHORING_OK");
        }

        public static void EnsurePlayerClearance()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var player=Object.FindFirstObjectByType<PaxPlayer>();
            var body=player.GetComponent<CharacterController>();
            if(!Mathf.Approximately(body.height,2) || !Mathf.Approximately(body.center.y,1)) return;
            Undo.RecordObject(body,"Add PAX tile doorway collision clearance");
            body.height=1.96f; body.center=Vector3.up*0.98f;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PAX_PLAYER_CLEARANCE_AUTHORING_OK");
        }
    }
}
