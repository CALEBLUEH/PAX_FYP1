using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pax.Tests
{
    public sealed class PaxPrototypeTests
    {
        private PaxPlayer player;
        private PaxWorld world;
        private PaxBuilder builder;
        private PaxCutaway cutaway;
        private PaxCamera rig;

        [UnitySetUp]
        public IEnumerator LoadIsland()
        {
            Time.captureDeltaTime = 1f/60;
            yield return SceneManager.LoadSceneAsync("PaxPrototype");
            Object.FindFirstObjectByType<PaxInput>().enabled = false;
            player = Object.FindFirstObjectByType<PaxPlayer>();
            world = Object.FindFirstObjectByType<PaxWorld>();
            builder = Object.FindFirstObjectByType<PaxBuilder>();
            cutaway = Object.FindFirstObjectByType<PaxCutaway>();
            rig = Object.FindFirstObjectByType<PaxCamera>();
            for (int i=0;i<10;i++) yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            if (player != null) player.SetCommand(Vector2.zero,false,false);
            yield return null;
        }
        [UnityTest]
        public IEnumerator MovementIsCameraRelativeAndDoesNotFallThroughGround()
        {
            Vector3 before = player.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;
            player.SetCommand(Vector2.up,false,false);
            for(int i=0;i<30;i++) yield return null;
            player.SetCommand(Vector2.zero,false,false);
            Vector3 delta = player.transform.position-before;
            Assert.That(Vector3.Dot(delta,forward),Is.GreaterThan(1.9f));
            Assert.That(Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,forward))),Is.LessThan(0.1f));
            Assert.That(player.transform.position.y,Is.InRange(-0.05f,0.1f));
            player.Warp(new Vector3(-1,0.03f,-1));
            rig.Orbit(90,0); rig.SnapToTarget();
            before=player.transform.position;
            player.SetCommand(Vector2.one,false,false);
            for(int i=0;i<30;i++) yield return null;
            player.SetCommand(Vector2.zero,false,false);
            Assert.That(Vector3.ProjectOnPlane(player.transform.position-before,Vector3.up).magnitude,Is.InRange(2.1f,2.35f),"Diagonal speed must remain normalized after rotation");
        }
        [UnityTest]
        public IEnumerator HeldJumpReachesTwoTilesAndTapIsLowerWithStableReference()
        {
            float start = player.transform.position.y;
            float reference = player.StableFeetHeight;
            float full = start;
            player.SetCommand(Vector2.zero,true,true);
            for(int i=0;i<65;i++)
            {
                yield return null;
                full = Mathf.Max(full,player.transform.position.y);
                if(i<35) Assert.That(player.StableFeetHeight,Is.EqualTo(reference).Within(0.02f));
            }
            player.SetCommand(Vector2.zero,false,false);
            Assert.That(full-start,Is.InRange(1.85f,2.05f));
            Assert.That(player.transform.position.y,Is.InRange(-0.05f,0.1f));
            float tap = player.transform.position.y;
            player.SetCommand(Vector2.zero,true,true);
            yield return null;
            player.SetCommand(Vector2.zero,false,false);
            for(int i=0;i<65;i++) { yield return null; tap = Mathf.Max(tap,player.transform.position.y); }
            Assert.That(tap-start,Is.LessThan((full-start)*0.65f));
        }
        [UnityTest]
        public IEnumerator SolidWallBlocksMovementWithoutAutomaticTileClimbing()
        {
            player.Warp(new Vector3(-1.5f,0.03f,-3));
            player.SetCommand(new Vector2(-0.7071f,-0.7071f),false,false);
            for(int i=0;i<60;i++) yield return null;
            Assert.That(player.transform.position.x,Is.GreaterThan(-2.12f));
            Assert.That(player.transform.position.y,Is.LessThan(0.1f));
        }
        [UnityTest]
        public IEnumerator CeilingAndRepeatedAirJumpRespectFullBodyClearance()
        {
            player.Warp(new Vector3(6,0.03f,-7));
            for(int i=0;i<5;i++) yield return null;
            float apex=0;
            player.SetCommand(Vector2.zero,true,true);
            for(int i=0;i<70;i++)
            {
                if(i==5 || i==10) player.SetCommand(Vector2.zero,true,true);
                yield return null;
                apex=Mathf.Max(apex,player.transform.position.y);
            }
            player.SetCommand(Vector2.zero,false,false);
            Assert.That(apex,Is.InRange(0.5f,1.05f),"Solid ceiling must stop a two-high player");
            Assert.That(player.transform.position.y,Is.LessThan(0.1f));
            player.Warp(new Vector3(0,0.03f,-3));
            world.TryAdd(new Vector3Int(0,1,-1));
            rig.Orbit(-45,0); rig.SnapToTarget();
            player.SetCommand(Vector2.up,false,false);
            for(int i=0;i<50;i++) yield return null;
            Assert.That(player.transform.position.z,Is.LessThan(-1.9f),"No crouching into one-high clearance");
        }
        [UnityTest]
        public IEnumerator AuthoredStepsUpdateGroundReferenceOnlyAfterRealLandings()
        {
            player.Warp(new Vector3(-8,0.03f,-7.2f));
            for(int i=0;i<8;i++) yield return null;
            Vector3 forward=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            var alongSteps=new Vector2(Vector3.Dot(Vector3.forward,right),Vector3.Dot(Vector3.forward,forward));
            for(int height=1;height<=2;height++)
            {
                float oldReference=player.StableFeetHeight;
                player.SetCommand(alongSteps,true,true);
                int movingFrames=height==1?16:13;
                for(int i=0;i<movingFrames;i++)
                {
                    yield return null;
                    Assert.That(player.StableFeetHeight,Is.EqualTo(oldReference).Within(0.02f),"Do not change the reference while ascending");
                }
                player.SetCommand(Vector2.zero,false,true);
                for(int i=0;i<55;i++) yield return null;
                Assert.IsTrue(player.Grounded,$"Must really land on step {height}");
                Assert.That(player.transform.position.y,Is.EqualTo(height).Within(0.06f));
                Assert.That(player.StableFeetHeight,Is.EqualTo(height).Within(0.06f));
            }
            rig.SnapToTarget();
            for(int i=0;i<30;i++) yield return null;
            Capture("Step-two-landing");
        }
        [UnityTest]
        public IEnumerator WalkThroughAuthoredTwoHighCaveEntrance()
        {
            Assert.IsTrue(world.Contains(new Vector3Int(7,2,3)),"The two-high entrance needs its authored lintel");
            Assert.IsFalse(world.Contains(new Vector3Int(7,1,3)));
            player.Warp(new Vector3(7.5f,0.03f,1));
            for(int i=0;i<8;i++) yield return null;
            Vector3 forward=Vector3.ProjectOnPlane(rig.transform.forward,Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            var intoCave=new Vector2(Vector3.Dot(Vector3.forward,right),Vector3.Dot(Vector3.forward,forward));
            player.SetCommand(intoCave,false,false);
            for(int i=0;i<60;i++) yield return null;
            player.SetCommand(Vector2.zero,false,false);
            Assert.That(player.transform.position.z,Is.GreaterThan(4.8f),"A two-high body must walk through the two-high door");
            Assert.That(player.transform.position.y,Is.EqualTo(0).Within(0.06f));
            Assert.IsTrue(player.Grounded);
            rig.SnapToTarget();
            for(int i=0;i<30;i++) yield return null;
            Capture("Cave-entry-walk");
        }
        [UnityTest]
        public IEnumerator MiningAndPlacementRespectSupportReachAndPlayerClearance()
        {
            var cell = new Vector3Int(-2,0,-3);
            Assert.IsTrue(builder.Place(cell));
            Assert.IsTrue(world.Contains(cell));
            Assert.IsFalse(builder.Place(cell));
            Assert.IsTrue(builder.Mine(cell));
            Assert.IsFalse(world.Contains(cell));
            Assert.IsFalse(builder.Place(new Vector3Int(0,0,-3)),"Must not place inside player");
            Assert.IsFalse(builder.Place(new Vector3Int(0,4,-3)),"No face support");
            Assert.IsFalse(builder.Place(new Vector3Int(-10,0,-3)),"Out of reach");
            Assert.IsFalse(builder.Mine(new Vector3Int(0,-2,-3)),"Protected foundation");
            yield return null;
        }
        [UnityTest]
        public IEnumerator PreciseCursorTargetsSurfaceAndProducesValidPreview()
        {
            builder.ToggleCursor();
            var cell = new Vector3Int(-3,1,-3);
            world.TryGet(cell,out var block);
            block.Present(1,Color.cyan);
            builder.Select(rig.View.WorldToScreenPoint(PaxGrid.Centre(cell)));
            Assert.That(builder.SelectedCell,Is.EqualTo(cell));
            Assert.That(builder.Mode,Does.StartWith("PRECISE"));
            Assert.IsTrue(builder.MiningPreview.gameObject.activeSelf);
            Assert.That(builder.MiningPreview.position,Is.EqualTo(PaxGrid.Centre(cell)));
            Assert.IsTrue(builder.PlacementPreview.gameObject.activeSelf);
            Assert.That(builder.PlacementPreview.position,Is.EqualTo(PaxGrid.Centre(builder.Placement)));
            Assert.IsTrue(builder.CanPlace);
            Assert.IsTrue(builder.PlacementUsesValidMaterial);
            var predictedPlacement=builder.Placement;
            Vector2 point=rig.View.WorldToScreenPoint(PaxGrid.Centre(cell));
            builder.SetPointer(point,false,true);
            yield return null;
            Assert.IsTrue(world.Contains(predictedPlacement),"Click must place precisely the previewed cell");
            builder.Select(point);
            var predictedMine=builder.SelectedCell;
            Assert.IsTrue(predictedMine.HasValue);
            builder.SetPointer(point,true,false);
            yield return null;
            Assert.IsFalse(world.Contains(predictedMine.Value),"Click must remove precisely the highlighted cell");
        }
        [UnityTest]
        public IEnumerator RotatedPrecisionAndVerticalReachRespectSolidLineOfSight()
        {
            player.Warp(new Vector3(0,0.03f,-1));
            rig.Orbit(90,0); rig.SnapToTarget();
            var overlapped=new Vector3Int(-2,0,-1);
            Assert.IsTrue(world.TryAdd(overlapped));
            Assert.IsTrue(world.TryGet(overlapped,out var overlappedTile));
            overlappedTile.Present(1,Color.cyan);
            builder.ToggleCursor();
            Physics.SyncTransforms();
            var screenHits=Physics.RaycastAll(rig.View.ScreenPointToRay(rig.View.WorldToScreenPoint(PaxGrid.Centre(overlapped))),100,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            System.Array.Sort(screenHits,(a,b)=>a.distance.CompareTo(b.distance));
            PaxBlock firstVisible=null;
            foreach(var hit in screenHits)
            {
                var hitTile=hit.collider.GetComponent<PaxBlock>();
                if(hitTile==null) continue;
                Debug.Log($"PAX_ROTATED_RAY: cell={hitTile.Cell} alpha={hitTile.Visibility:F2} reach={PaxGrid.InReach(player.Chest,hitTile.Cell,5)} chestLOS={builder.HasLineOfSight(hitTile.Cell)}");
                if(firstVisible==null && hitTile.Visibility>=0.12f) firstVisible=hitTile;
            }
            Assert.NotNull(firstVisible);
            Assert.That(firstVisible.Cell,Is.Not.EqualTo(overlapped),"House roof should overlap this tile in camera space");
            builder.Select(rig.View.WorldToScreenPoint(PaxGrid.Centre(overlapped)));
            Assert.IsNull(builder.SelectedCell,"An out-of-reach visible roof blocks the west-side precision ray");
            // West-side rays cross the house roof after this rotation. Use the open east side
            // for the positive precision case; solids in screen space must not be ignored.
            var target=new Vector3Int(2,0,-1);
            Assert.IsTrue(world.TryAdd(target));
            Assert.IsTrue(world.TryGet(target,out var tile));
            tile.Present(1,Color.cyan);
            Physics.SyncTransforms();
            builder.Select(rig.View.WorldToScreenPoint(PaxGrid.Centre(target)));
            Assert.That(builder.SelectedCell,Is.EqualTo(target));
            Assert.IsTrue(builder.MiningPreview.gameObject.activeSelf);
            player.Warp(new Vector3(0,0.03f,-3));
            Assert.IsTrue(world.TryAdd(new Vector3Int(-1,0,-3)));
            yield return null; // Let authored runtime collider changes enter the physics broadphase.
            Assert.IsFalse(builder.Mine(new Vector3Int(-3,0,-3)),"A solid wall blocks chest-to-target line of sight");
            Assert.IsFalse(builder.CanPlaceAt(new Vector3Int(-2,0,-3)),"Do not build through a solid wall");
            for(int y=0;y<=6;y++) Assert.IsTrue(world.TryAdd(new Vector3Int(2,y,-3)));
            Assert.IsFalse(builder.Mine(new Vector3Int(2,6,-3)),"Vertical range contributes to the five-tile limit");
            yield return null;
        }
        [UnityTest]
        public IEnumerator FadingAndCameraRotationDoNotTransmitSunlightThroughSolids()
        {
            var lighting=Object.FindFirstObjectByType<PaxWorldLighting>();
            Assert.NotNull(lighting);
            lighting.RefreshNow();
            Assert.IsTrue(world.TryGet(new Vector3Int(-6,-1,6),out var indoor));
            Assert.IsTrue(world.TryGet(new Vector3Int(-11,-1,-9),out var outdoor));
            Assert.NotNull(indoor); Assert.NotNull(outdoor);
            Assert.That(indoor.Sunlight,Is.EqualTo(0));
            Assert.That(outdoor.Sunlight,Is.EqualTo(1));
            float before=indoor.Sunlight;
            foreach(var block in world.Blocks) block.Present(0,Color.cyan,0);
            rig.Orbit(180,0); rig.SnapToTarget();
            lighting.RefreshNow();
            Assert.That(indoor.Sunlight,Is.EqualTo(before));
            Assert.That(outdoor.Sunlight,Is.EqualTo(1));
            yield return null;
        }
        [UnityTest]
        public IEnumerator MovingPlayerCannotBecomeCachedWorldSunlightOcclusion()
        {
            var lighting=Object.FindFirstObjectByType<PaxWorldLighting>();
            Assert.NotNull(lighting);
            Assert.IsTrue(world.TryGet(new Vector3Int(-11,-1,-9),out var sample));
            lighting.RefreshNow();
            Assert.That(sample.Sunlight,Is.EqualTo(1));
            int oldLayer=player.gameObject.layer;
            player.gameObject.layer=0; // Prove filtering even if an author changes the normal Ignore Raycast layer.
            try
            {
                var sun=RenderSettings.sun;
                Assert.NotNull(sun);
                Vector3 direction=-sun.transform.forward;
                Vector3 top=PaxGrid.Centre(sample.Cell)+Vector3.up*0.505f;
                player.Warp(new Vector3(top.x+direction.x,0.03f,top.z+direction.z));
                Physics.SyncTransforms();
                Assert.IsTrue(Physics.Raycast(top,direction,out var actorHit,50,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore));
                Assert.That(actorHit.collider,Is.EqualTo(player.GetComponent<CharacterController>()),"Fixture must really put the actor in the light ray");
                lighting.RefreshNow();
                Assert.That(sample.Sunlight,Is.EqualTo(1),"Moving actors are not logical world tiles");
                player.Warp(new Vector3(0,0.03f,-3));
                Assert.That(sample.Sunlight,Is.EqualTo(1),"No stale actor shadow after movement without a world revision");
            }
            finally { player.gameObject.layer=oldLayer; }
            yield return null;
        }
        [UnityTest]
        public IEnumerator FadedSolidCanBeExplicitlyTargetedWithoutRestoringItsOpacity()
        {
            builder.ToggleCursor();
            var cell = new Vector3Int(-3,1,-3);
            world.TryGet(cell,out var block);
            cutaway.PresentationEnabled=false;
            block.Present(0.04f,Color.cyan);
            Vector2 point=rig.View.WorldToScreenPoint(PaxGrid.Centre(cell));
            builder.Select(point);
            Assert.That(builder.SelectedCell,Is.Not.EqualTo(cell));
            builder.ToggleGhostTargets();
            builder.Select(point);
            Assert.That(builder.SelectedCell,Is.EqualTo(cell));
            Assert.That(block.Visibility,Is.EqualTo(0.04f));
            Assert.That(builder.Status,Does.Contain("Layer +1"));
            Assert.IsTrue(builder.Mine(cell));
            yield return null;
        }
        [UnityTest]
        public IEnumerator UndergroundCutawayAndGhostsStayLocal()
        {
            player.Warp(new Vector3(0,-2.97f,7));
            for(int i=0;i<10;i++) yield return null;
            rig.SnapToTarget(); cutaway.RefreshNow();
            world.TryGet(new Vector3Int(-9,3,9),out var distant);
            world.TryGet(new Vector3Int(-1,-1,7),out var local);
            Assert.NotNull(distant); Assert.NotNull(local);
            Assert.That(cutaway.TargetFor(distant),Is.EqualTo(1));
            Assert.That(cutaway.GhostRelevance(distant),Is.EqualTo(0));
            Assert.That(cutaway.TargetFor(local),Is.LessThan(1));
            Assert.That(cutaway.GhostRelevance(local),Is.GreaterThan(0));
            for(int i=0;i<45;i++) yield return null;
            Capture("Underground-shaft");
        }
        [UnityTest]
        public IEnumerator ActualObstructionOverridesNearbyAndRoofPreservationWithoutChangingCollision()
        {
            player.Warp(new Vector3(-6,0.03f,6));
            yield return null;
            rig.SnapToTarget();
            Physics.SyncTransforms();
            cutaway.RefreshNow();
            Assert.That(cutaway.ObstructionCount,Is.GreaterThan(0));
            Assert.That(cutaway.RoofCoverage,Is.GreaterThan(0.5f));
            int checkedBlocks=0;
            foreach(var block in world.Blocks)
            {
                if(!cutaway.IsObstruction(block)) continue;
                Assert.That(cutaway.TargetFor(block),Is.LessThanOrEqualTo(0.04f));
                Assert.IsTrue(block.Solid.enabled);
                Assert.NotNull(block.SolidShadow);
                Assert.That(block.SolidShadow.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
                checkedBlocks++;
            }
            Assert.That(checkedBlocks,Is.GreaterThan(0));
        }
        [UnityTest]
        public IEnumerator HouseCaveAndLowRoomIncreaseRoofCoverageAndFloodStaysBounded()
        {
            player.Warp(new Vector3(0,0.03f,-3));
            cutaway.RefreshNow();
            float openCoverage = cutaway.RoofCoverage;
            Capture("Open-field");
            int index=0;
            foreach(var location in new[]{new Vector3(-6,0.03f,6),new Vector3(7,0.03f,7),new Vector3(6,0.03f,-7)})
            {
                player.Warp(location);
                yield return null;
                rig.SnapToTarget(); cutaway.RefreshNow();
                Assert.That(cutaway.RoofCoverage,Is.GreaterThan(openCoverage+0.3f));
                Assert.That(cutaway.Confinement,Is.GreaterThan(0.15f));
                Assert.That(cutaway.FloodVisited,Is.LessThanOrEqualTo(192));
                for(int i=0;i<45;i++) yield return null;
                Capture(new[]{"House-interior","Cave-interior","Low-room-interior"}[index++]);
            }
        }
        [UnityTest]
        public IEnumerator HighHallAndNarrowPassageProvideDifferentLocalContext()
        {
            player.Warp(new Vector3(0,0.03f,-7));
            rig.SnapToTarget(); cutaway.RefreshNow();
            float highGap=cutaway.RoofGap;
            for(int i=0;i<45;i++) yield return null;
            Capture("High-hall");
            player.Warp(new Vector3(3,0.03f,-7));
            rig.SnapToTarget(); cutaway.RefreshNow();
            Assert.That(cutaway.FloodVisited,Is.LessThanOrEqualTo(192));
            for(int i=0;i<45;i++) yield return null;
            Capture("Narrow-passage");
            player.Warp(new Vector3(6,0.03f,-7));
            cutaway.RefreshNow();
            Assert.That(cutaway.RoofGap,Is.LessThan(highGap));
        }
        [UnityTest]
        public IEnumerator CameraRotationRestoresOldBlockersAndRoofEditsRefreshContext()
        {
            player.Warp(new Vector3(-6,0.03f,6));
            rig.SnapToTarget(); cutaway.RefreshNow();
            var previous=new System.Collections.Generic.List<PaxBlock>();
            foreach(var block in world.Blocks) if(cutaway.IsObstruction(block)) previous.Add(block);
            rig.Orbit(180,0); rig.SnapToTarget(); cutaway.RefreshNow();
            bool restored=false;
            foreach(var block in previous) if(!cutaway.IsObstruction(block) && cutaway.TargetFor(block)>0.04f) restored=true;
            Assert.IsTrue(restored);
            Assert.That(cutaway.ObstructionCount,Is.GreaterThan(0));
            for(int i=0;i<45;i++) yield return null;
            Capture("House-rotated");
            player.Warp(new Vector3(6,0.03f,-7));
            cutaway.RefreshNow(); float coverage=cutaway.RoofCoverage;
            Assert.IsTrue(builder.Mine(new Vector3Int(6,3,-7)));
            yield return null;
            Assert.That(cutaway.RoofCoverage,Is.LessThan(coverage));
            Assert.IsTrue(builder.Place(new Vector3Int(6,3,-7)));
            yield return null;
            Assert.That(cutaway.RoofCoverage,Is.EqualTo(coverage).Within(0.01f));
        }
        [UnityTest]
        public IEnumerator CutawayFadesAndRestoresWithoutRemovingWorldSolidity()
        {
            player.Warp(new Vector3(-6,0.03f,6));
            rig.SnapToTarget(); cutaway.RefreshNow();
            PaxBlock obstruction = null;
            foreach(var block in world.Blocks) if(cutaway.IsObstruction(block)) { obstruction=block; break; }
            Assert.NotNull(obstruction);
            obstruction.Present(1,Color.cyan);
            yield return null;
            Assert.That(obstruction.Visibility,Is.GreaterThan(0.04f).And.LessThan(1));
            for(int i=0;i<60;i++) yield return null;
            Assert.That(obstruction.Visibility,Is.LessThan(0.06f));
            cutaway.enabled=false;
            Assert.That(obstruction.Visibility,Is.EqualTo(1));
            Assert.IsTrue(obstruction.Solid.enabled);
        }
        [UnityTest]
        public IEnumerator CameraZoomOrbitAndRespawnRecoverWithoutWorldReset()
        {
            float zoom=rig.View.orthographicSize;
            rig.Zoom(2); Assert.That(rig.View.orthographicSize,Is.EqualTo(zoom-2));
            Quaternion rotation=rig.transform.rotation;
            rig.Orbit(90,0); rig.SnapToTarget();
            Assert.That(Quaternion.Angle(rotation,rig.transform.rotation),Is.GreaterThan(80));
            player.Warp(new Vector3(0,-10,-3));
            yield return null;
            Assert.That(player.transform.position.y,Is.GreaterThan(-1));
            Assert.That(world.Blocks.Count,Is.GreaterThan(1000));
        }

        private void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("Logs/PAX/Captures");
            var output = new RenderTexture(1600,900,24);
            var texture = new Texture2D(1600,900,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            var previousTarget=rig.View.targetTexture;
            try
            {
                rig.View.targetTexture=output;
                rig.View.Render();
                RenderTexture.active=output;
                texture.ReadPixels(new Rect(0,0,1600,900),0,0);
                texture.Apply();
                File.WriteAllBytes("Logs/PAX/Captures/"+name+".png",texture.EncodeToPNG());
            }
            finally
            {
                rig.View.targetTexture=previousTarget;
                RenderTexture.active=previous;
                Object.Destroy(texture); output.Release(); Object.Destroy(output);
            }
        }
    }
}
