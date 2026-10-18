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
        [SerializeField] private string emberScenePath;
        [SerializeField] private PaxContinueCatalog continueCatalog;
        [SerializeField, Min(0.5f)] private float saveRefreshSeconds = 2;
        [SerializeField] private KeyCode continueResourceKey = KeyCode.Alpha4, continueChartKey = KeyCode.Alpha5, continueEmberKey = KeyCode.Alpha6;
        [SerializeField] private KeyCode emberKey = KeyCode.Alpha3;
        [SerializeField] private KeyCode resourceKey = KeyCode.Alpha1, chartKey = KeyCode.Alpha2;
        [SerializeField] private string readyMessage = "Both expeditions are available. Choose a fresh expedition.";
        [SerializeField] private string releaseMessage = "Release held keys and mouse buttons to choose.";
        [SerializeField] private string focusMessage = "Return to this window, then release held controls to choose.";
        [SerializeField] private string loadingMessage = "Opening a fresh expedition… No snapshot is loaded automatically.";
        [SerializeField] private string unavailableMessage = "This authored scene is unavailable in the build.";
        private Button resource, chart, ember;
        private bool emberWasHeld;
        private readonly bool[] continueWasHeld = new bool[3], available = new bool[3];
        private readonly string[] availabilityReasons = new string[3];
        private Button[] continueButtons; private Label[] saveLabels;
        private string validationDirectory; private float nextSaveRefresh; private bool continuing;
        public PaxContinueCatalog ContinueCatalog => continueCatalog;
        public bool CanContinue(int number) => number >= 1 && number <= 3 && available[number - 1];
        public string ContinueReason(int number) => availabilityReasons[number - 1];
        private Label status;
        private bool focused = true, awaitingRelease = true, loading, hardware = true, resourceWasHeld, chartWasHeld, focusPending = true;
        private int transitionFrame = -1;
        private string error;
        public bool Ready => isActiveAndEnabled && focused && !awaitingRelease && !loading && Time.frameCount > transitionFrame;
        public bool Loading => loading;
        public string ResourceScenePath => resourceScenePath;
        public string ChartScenePath => chartScenePath;
        public string EmberScenePath => emberScenePath;
        public void Configure(UIDocument ui, string resourcePath, string chartPath)
        { document = ui; resourceScenePath = resourcePath; chartScenePath = chartPath; }
        private void Awake() { if (!Application.isBatchMode) focused = Application.isFocused; }
        private void Start()
        {
            resource = document.rootVisualElement.Q<Button>("select-resource"); chart = document.rootVisualElement.Q<Button>("select-chart");
            ember = document.rootVisualElement.Q<Button>("select-ember");
            continueButtons = new[] { document.rootVisualElement.Q<Button>("continue-resource"), document.rootVisualElement.Q<Button>("continue-chart"), document.rootVisualElement.Q<Button>("continue-ember") };
            saveLabels = new[] { document.rootVisualElement.Q<Label>("saved-resource"), document.rootVisualElement.Q<Label>("saved-chart"), document.rootVisualElement.Q<Label>("saved-ember") };
            continueButtons[0].clicked += ContinueResource; continueButtons[1].clicked += ContinueChart; continueButtons[2].clicked += ContinueEmber;
            status = document.rootVisualElement.Q<Label>("selection-status");
            resource.clicked += OnResource; chart.clicked += OnChart; ember.clicked += OnEmber; RefreshSaves(); Present();
        }
        private void Update()
        {
            if (!loading && focused && Time.unscaledTime >= nextSaveRefresh) RefreshSaves();
            if (hardware) ProcessInput(Input.GetKey(resourceKey), Input.GetKey(chartKey), Input.GetKey(emberKey), Input.anyKey || Input.mouseScrollDelta.sqrMagnitude > 0, Input.GetKey(continueResourceKey), Input.GetKey(continueChartKey), Input.GetKey(continueEmberKey));
            else Present();
        }
        public void ProcessInput(bool resourceHeld, bool chartHeld, bool anyHeld)
            => ProcessInput(resourceHeld, chartHeld, false, anyHeld);
        public void ProcessInput(bool resourceHeld, bool chartHeld, bool emberHeld, bool anyHeld)
            => ProcessInput(resourceHeld, chartHeld, emberHeld, anyHeld, false, false, false);
        public void ProcessInput(bool resourceHeld, bool chartHeld, bool emberHeld, bool anyHeld, bool resourceContinue, bool chartContinue, bool emberContinue)
        {
            bool first = resourceHeld && !resourceWasHeld, second = chartHeld && !chartWasHeld;
            bool third = emberHeld && !emberWasHeld; emberWasHeld = emberHeld;
            resourceWasHeld = resourceHeld; chartWasHeld = chartHeld;
            bool c1 = resourceContinue && !continueWasHeld[0], c2 = chartContinue && !continueWasHeld[1], c3 = emberContinue && !continueWasHeld[2];
            continueWasHeld[0] = resourceContinue; continueWasHeld[1] = chartContinue; continueWasHeld[2] = emberContinue;
            if (!isActiveAndEnabled || !focused || loading) { Present(); return; }
            if (awaitingRelease && !anyHeld && !resourceHeld && !chartHeld && !emberHeld && !resourceContinue && !chartContinue && !emberContinue)
            { awaitingRelease = false; transitionFrame = Time.frameCount; }
            if (Ready) { if (first) LaunchResource(); else if (second) LaunchChart(); else if (third) LaunchEmber(); else if (c1) Continue(1); else if (c2) Continue(2); else if (c3) Continue(3); }
            Present();
        }
        public bool LaunchResource() => Launch(resourceScenePath);
        public bool LaunchChart() => Launch(chartScenePath);
        public bool LaunchEmber() => Launch(emberScenePath);
        private void OnEmber() => LaunchEmber();
        private void OnResource() => LaunchResource();
        private void OnChart() => LaunchChart();
        private void ContinueResource() => Continue(1);
        private void ContinueChart() => Continue(2);
        private void ContinueEmber() => Continue(3);
        private string SceneFor(int number) => number == 1 ? resourceScenePath : number == 2 ? chartScenePath : emberScenePath;
        public void UseValidationDirectory(string directory)
        {
            if (!Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), "-paxSmoke") < 0) throw new InvalidOperationException("Storage override requires Editor or opt-in validation.");
            validationDirectory = System.IO.Path.GetFullPath(directory); nextSaveRefresh = 0;
        }
        public void RefreshSaves()
        {
            nextSaveRefresh = Time.unscaledTime + saveRefreshSeconds;
            bool diagnostic = Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-paxSmoke") >= 0;
            for (int i = 0; i < 3; i++)
            {
                available[i] = false; string hint = "No compatible snapshot. Start fresh; F5 saves world + stocks.";
                var slot = continueCatalog != null ? continueCatalog.ForScene(SceneFor(i + 1)) : null;
                if (diagnostic && validationDirectory == null) availabilityReasons[i] = "Diagnostic storage not selected; human save folders are not read.";
                else if (slot == null) availabilityReasons[i] = "Saved-world definition unavailable; fresh play remains available.";
                else
                {
                    try { available[i] = PaxContinueRead.Best(slot.PathFor(validationDirectory), slot.Validate, out var candidate, out string reason); availabilityReasons[i] = reason; if (available[i]) hint = candidate.Previous ? "Previous snapshot recoverable. Journal starts fresh." : "Current world/resources ready. Journal starts fresh."; }
                    catch (Exception failure) when (PaxSaveFile.IsExpectedFailure(failure)) { availabilityReasons[i] = failure.Message; }
                }
                if (saveLabels != null) saveLabels[i].text = hint;
            }
            Present();
        }
        public bool Continue(int number)
        {
            if (!Ready || number < 1 || number > 3 || hardware && !Application.isBatchMode && !Application.isFocused) return false;
            RefreshSaves(); string path = SceneFor(number); var slot = continueCatalog != null ? continueCatalog.ForScene(path) : null;
            if (!available[number - 1] || slot == null || !Application.CanStreamedLevelBeLoaded(path))
            { error = "Continue unavailable for this expedition: no compatible current/previous snapshot. Saved files kept; choose a fresh start to play."; Present(); return false; }
            if (FindFirstObjectByType<PaxContinueTransfer>() != null) return false;
            loading = continuing = true; Present();
            new GameObject("Explicit PAX Continue transition").AddComponent<PaxContinueTransfer>().Configure(continueCatalog, slot, validationDirectory, !hardware); return true;
        }
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
            ember.SetEnabled(Ready);
            if (continueButtons != null) for (int i = 0; i < 3; i++) continueButtons[i].SetEnabled(Ready && available[i]);
            status.text = !focused ? focusMessage : loading ? continuing ? "Continuing saved world/resources… Rechecking at camp; journal starts fresh." : loadingMessage : !Ready ? releaseMessage : error ?? readyMessage;
            if (Ready && focusPending) { resource.Focus(); focusPending = false; }
        }
        public void ProcessFocus(bool hasFocus)
        {
            if (!isActiveAndEnabled) return;
            focused = hasFocus; awaitingRelease = true; transitionFrame = Time.frameCount;
            resourceWasHeld = chartWasHeld = true; focusPending = true; Present();
            emberWasHeld = true;
            for (int i = 0; i < 3; i++) continueWasHeld[i] = true;
            if (hasFocus && !loading) RefreshSaves();
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
            if (ember != null) ember.clicked -= OnEmber;
            if (continueButtons != null) { continueButtons[0].clicked -= ContinueResource; continueButtons[1].clicked -= ContinueChart; continueButtons[2].clicked -= ContinueEmber; }
        }
    }
}
