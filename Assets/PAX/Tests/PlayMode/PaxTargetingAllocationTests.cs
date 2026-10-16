using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pax.Tests
{
    public sealed class PaxTargetingAllocationTests
    {
        [UnityTest]
        public IEnumerator MeasureStableTargetingAllocations()
        {
            Time.captureDeltaTime = 1f / 60;
            yield return SceneManager.LoadSceneAsync("PaxPrototype");
            Object.FindFirstObjectByType<PaxInput>().enabled = false;
            var builder = Object.FindFirstObjectByType<PaxBuilder>();
            builder.enabled = false;
            Object.FindFirstObjectByType<PaxCutaway>().enabled = false;
            Object.FindFirstObjectByType<PaxHud>().enabled = false;
            for (int i = 0; i < 10; i++) yield return null;
            var camera = Object.FindFirstObjectByType<PaxCamera>().View;
            Vector2 point = camera.WorldToScreenPoint(PaxGrid.Centre(new Vector3Int(-3, 1, -3)));
            var records = new string[2];
            using (var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 16))
            {
                Assert.IsTrue(allocations.Valid);
                Assert.That(allocations.UnitType.ToString(), Is.EqualTo("Bytes"));
                yield return null;
                yield return null;
                long emptyControl = allocations.LastValue;
                long threadBefore = GC.GetAllocatedBytesForCurrentThread();
                var allocation = new byte[262144];
                long threadControl = GC.GetAllocatedBytesForCurrentThread() - threadBefore;
                yield return null;
                GC.KeepAlive(allocation);
                long engineControl = allocations.LastValue - emptyControl;
                UnityEngine.Debug.Log($"PAX_ALLOCATION_CONTROL: managed counter={threadControl}; Unity frame-counter delta={engineControl}; unit={allocations.UnitType}");
                Assert.That(engineControl, Is.GreaterThanOrEqualTo(262144), "Counter must detect a known allocation before reporting optimization results");
                for (int mode = 0; mode < 2; mode++)
                {
                    if (mode == 1) builder.ToggleCursor();
                    for (int warm = 0; warm < 20; warm++) builder.Select(point);
                    Assert.IsTrue(builder.SelectedCell.HasValue);
                    var selected = builder.SelectedCell;
                    var clock = new Stopwatch();
                    const int iterations = 500;
                    yield return null; // Flush warm-up allocations.
                    yield return null; // Matched frame without selection work.
                    long emptyFrame = allocations.LastValue;
                    clock.Start();
                    for (int i = 0; i < iterations; i++) builder.Select(point);
                    clock.Stop();
                    yield return null;
                    long bytes = Math.Max(0, allocations.LastValue - emptyFrame);
                    Assert.That(builder.SelectedCell, Is.EqualTo(selected));
                    Assert.That(bytes / (double)iterations, Is.LessThan(200), "Stable targeting must not rebuild its feedback strings on every call");
                    records[mode] = string.Format(CultureInfo.InvariantCulture,
                        "{0}: {1} calls; {2} excess frame bytes over {5} idle bytes; {3:F2} estimated bytes/call; {4:F3} ms", builder.Mode, iterations, bytes, bytes / (double)iterations, clock.Elapsed.TotalMilliseconds, emptyFrame);
                    UnityEngine.Debug.Log("PAX_TARGETING_MEASURE: " + records[mode]);
                }
            }
            Directory.CreateDirectory("Logs/PAX");
            File.WriteAllLines("Logs/PAX/TargetingAllocation.txt", records);
            Time.captureDeltaTime = 0;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            yield return null;
        }
    }
}
