using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pax
{
    public sealed class PaxExpeditionSelection : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private string resourceScenePath;
        [SerializeField] private string chartScenePath;
        [SerializeField] private KeyCode resourceKey = KeyCode.Alpha1, chartKey = KeyCode.Alpha2;
        [SerializeField] private string readyMessage = "Both expeditions are available. Choose a fresh expedition.";
        [SerializeField] private string releaseMessage = "Release held keys and mouse buttons to choose.";
        [SerializeField] private string focusMessage = "Return to this window, then release held controls to choose.";
        [SerializeField] private string loadingMessage = "Opening a fresh expedition… No snapshot is loaded automatically.";
        [SerializeField] private string unavailableMessage = "This authored scene is unavailable in the build.";
        private Button resource, chart;
        private Label status;
        private bool focused = true, awaitingRelease = true, loading, hardware = true, resourceWasHeld, chartWasHeld, focusPending = true;
        private int transitionFrame = -1;
        private string error;
        public bool Ready => isActiveAndEnabled && focused && !awaitingRelease && !loading && Time.frameCount > transitionFrame;
        public bool Loading => loading;
        public string ResourceScenePath => resourceScenePath;
        public string ChartScenePath => chartScenePath;
        public void Configure(UIDocument ui, string resourcePath, string chartPath)
        { document = ui; resourceScenePath = resourcePath; chartScenePath = chartPath; }
        private void Awake() { if (!Application.isBatchMode) focused = Application.isFocused; }
        private void Start()
        {
            resource = document.rootVisualElement.Q<Button>("select-resource"); chart = document.rootVisualElement.Q<Button>("select-chart");
            status = document.rootVisualElement.Q<Label>("selection-status");
            resource.clicked += OnResource; chart.clicked += OnChart; Present();
        }
        private void Update()
        {
            if (hardware) ProcessInput(Input.GetKey(resourceKey), Input.GetKey(chartKey), Input.anyKey || Input.mouseScrollDelta.sqrMagnitude > 0);
            else Present();
        }
        public void ProcessInput(bool resourceHeld, bool chartHeld, bool anyHeld)
        {
            bool first = resourceHeld && !resourceWasHeld, second = chartHeld && !chartWasHeld;
            resourceWasHeld = resourceHeld; chartWasHeld = chartHeld;
            if (!isActiveAndEnabled || !focused || loading) { Present(); return; }
            if (awaitingRelease && !anyHeld && !resourceHeld && !chartHeld)
            { awaitingRelease = false; transitionFrame = Time.frameCount; }
            if (Ready) { if (first) LaunchResource(); else if (second) LaunchChart(); }
            Present();
        }
        public bool LaunchResource() => Launch(resourceScenePath);
        public bool LaunchChart() => Launch(chartScenePath);
        private void OnResource() => LaunchResource();
        private void OnChart() => LaunchChart();
        private bool Launch(string path)
        {
            if (!Ready || hardware && !Application.isBatchMode && !Application.isFocused) return false;
            if (string.IsNullOrWhiteSpace(path) || !Application.CanStreamedLevelBeLoaded(path)) { error = unavailableMessage; Present(); return false; }
            loading = true; Present(); SceneManager.LoadSceneAsync(path, LoadSceneMode.Single); return true;
        }
        private void Present()
        {
            if (resource == null) return;
            resource.SetEnabled(Ready); chart.SetEnabled(Ready);
            status.text = !focused ? focusMessage : loading ? loadingMessage : !Ready ? releaseMessage : error ?? readyMessage;
            if (Ready && focusPending) { resource.Focus(); focusPending = false; }
        }
        public void ProcessFocus(bool hasFocus)
        {
            if (!isActiveAndEnabled) return;
            focused = hasFocus; awaitingRelease = true; transitionFrame = Time.frameCount;
            resourceWasHeld = chartWasHeld = true; focusPending = true; Present();
        }
        private void OnApplicationFocus(bool hasFocus) { if (hardware) ProcessFocus(hasFocus); }
        private void OnApplicationPause(bool paused) { if (hardware) ProcessFocus(!paused && Application.isFocused); }
        public void UseValidationInput()
        {
            if (!Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), "-paxSmoke") < 0) throw new InvalidOperationException("Input override requires Editor or opt-in validation.");
            hardware = false; focused = true; awaitingRelease = true; transitionFrame = Time.frameCount; focusPending = true;
        }
        private void OnDisable()
        {
            if (resource != null) resource.clicked -= OnResource;
            if (chart != null) chart.clicked -= OnChart;
        }
    }
}
