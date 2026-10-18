using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Pax
{
    // Scene-owned pause clock and input transition. Earlier scenes have no owner and keep their controls.
    [DefaultExecutionOrder(-300)]
    public sealed class PaxPause : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private PaxBuilder builder;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField, TextArea] private string missionHelp;
        [SerializeField] private string pausedMessage = "Paused / Controls";
        [SerializeField] private string focusMessage = "Focus lost / Paused";
        [SerializeField] private string selectionScenePath;
        [SerializeField] private string returnUnavailableMessage = "Selection scene unavailable; expedition kept paused.";
        private VisualElement overlay;
        private Label title;
        private Button resume;
        private VisualElement helpCard, confirmation;
        private Button returnSelection, cancelReturn, confirmReturn;
        private Label returnStatus;
        private bool confirmingReturn, returning, uiAwaitingRelease;
        private int uiTransitionFrame = -1;
        public bool ConfirmingReturn => confirmingReturn;
        public bool Returning => returning;
        public string SelectionScenePath => selectionScenePath;
        private bool UiReady => focused && (!hardwareInput || Application.isBatchMode || Application.isFocused) && !returning && !uiAwaitingRelease && Time.frameCount > uiTransitionFrame;
        private bool paused, ownsClock, awaitingRelease, escapeWasHeld, focused = true;
        private bool hardwareInput = true;
        private bool started;
        private float previousScale;
        private int transitionFrame = -1;
        public bool IsPaused => isActiveAndEnabled && paused;
        public bool BlocksActions => isActiveAndEnabled && (paused || !focused || awaitingRelease || Time.frameCount <= transitionFrame);
        public bool AwaitingRelease => awaitingRelease;
        public void Configure(UIDocument ui, PaxPlayer motor, PaxBuilder interaction, string help)
        { document = ui; player = motor; builder = interaction; missionHelp = help; }

        private void OnEnable() { if (started) BindView(); }
        private void Start()
        {
            started = true; awaitingRelease = true; transitionFrame = Time.frameCount; BindView();
            if (hardwareInput && !Application.isBatchMode && !Application.isFocused) ProcessFocus(false);
        }
        private void BindView()
        {
            Unbind();
            overlay = document.rootVisualElement.Q("pause-overlay");
            title = document.rootVisualElement.Q<Label>("pause-title");
            resume = document.rootVisualElement.Q<Button>("pause-resume");
            helpCard = document.rootVisualElement.Q("pause-card"); confirmation = document.rootVisualElement.Q("return-confirmation");
            returnSelection = document.rootVisualElement.Q<Button>("pause-selection"); cancelReturn = document.rootVisualElement.Q<Button>("return-cancel"); confirmReturn = document.rootVisualElement.Q<Button>("return-confirm");
            returnStatus = document.rootVisualElement.Q<Label>("return-status");
            document.rootVisualElement.Q<Label>("pause-mission").text = missionHelp;
            resume.clicked += Resume;
            returnSelection.clicked += RequestReturn; cancelReturn.clicked += CancelReturn; confirmReturn.clicked += ConfirmReturn;
            Present();
        }
        private void Update()
        {
            if (hardwareInput) ProcessInput(Input.GetKey(pauseKey), Input.anyKey || Input.mouseScrollDelta.sqrMagnitude > 0);
        }
        // The same transition used by the hardware adapter; validation supplies held-state frames.
        public void ProcessInput(bool escapeHeld, bool gameplayHeld)
        {
            if (!isActiveAndEnabled || !focused) return;
            bool pressed = escapeHeld && !escapeWasHeld;
            escapeWasHeld = escapeHeld;
            if (pressed) { if (confirmingReturn) CancelReturn(); else if (paused) Resume(); else Pause(); }
            if (uiAwaitingRelease && !escapeHeld && !gameplayHeld && Time.frameCount > uiTransitionFrame)
            { uiAwaitingRelease = false; uiTransitionFrame = Time.frameCount; }
            if (paused && UiReady)
            { bool focusPending = confirmingReturn ? !cancelReturn.enabledSelf : !resume.enabledSelf; Present(); if (focusPending) { if (confirmingReturn) cancelReturn.Focus(); else resume.Focus(); } }
            if (!paused && awaitingRelease && !escapeHeld && !gameplayHeld && Time.frameCount > transitionFrame)
            { awaitingRelease = false; transitionFrame = Time.frameCount; }
        }
        public void Pause()
        {
            if (!isActiveAndEnabled || paused) return;
            previousScale = Time.timeScale;
            ownsClock = true; paused = true; Time.timeScale = 0;
            player.ClearCommand(); builder.ClearPendingActions();
            Present();
            resume?.Focus();
        }
        public void Resume()
        {
            if (!isActiveAndEnabled || !paused || !UiReady || confirmingReturn) return;
            paused = false; awaitingRelease = true; transitionFrame = Time.frameCount;
            player.ClearCommand(); builder.ClearPendingActions();
            ReleaseClock(); Present();
        }
        private void ReleaseClock()
        {
            if (!ownsClock) return;
            Time.timeScale = previousScale; ownsClock = false;
        }
        private void Present()
        {
            if (overlay == null) return;
            overlay.style.display = paused ? DisplayStyle.Flex : DisplayStyle.None;
            title.text = focused ? pausedMessage : focusMessage;
            helpCard.style.display = confirmingReturn ? DisplayStyle.None : DisplayStyle.Flex;
            confirmation.style.display = confirmingReturn ? DisplayStyle.Flex : DisplayStyle.None;
            resume.SetEnabled(UiReady); returnSelection.SetEnabled(UiReady && !string.IsNullOrEmpty(selectionScenePath));
            cancelReturn.SetEnabled(UiReady); confirmReturn.SetEnabled(UiReady);
            returnStatus.text = !focused ? focusMessage : returning ? "Returning to selection… Saved files are retained." : "Only confirm if you want to discard this live expedition.";
        }
        public void ProcessFocus(bool hasFocus)
        {
            if (!isActiveAndEnabled) return;
            focused = hasFocus;
            // Regaining focus never resumes or interprets an already-held Escape as a fresh press.
            escapeWasHeld = true;
            if (!focused) Pause();
            if (confirmingReturn) { uiAwaitingRelease = true; uiTransitionFrame = Time.frameCount; }
            Present();
            if (focused && paused && !confirmingReturn) resume?.Focus();
        }
        public void RequestReturn()
        {
            if (!isActiveAndEnabled || !paused || !UiReady || confirmingReturn || string.IsNullOrEmpty(selectionScenePath)) return;
            confirmingReturn = true; uiAwaitingRelease = true; uiTransitionFrame = Time.frameCount;
            builder.ClearPendingActions(); Present();
        }
        public void CancelReturn()
        {
            if (!isActiveAndEnabled || !paused || !focused || !confirmingReturn || returning) return;
            confirmingReturn = false; uiAwaitingRelease = true; uiTransitionFrame = Time.frameCount;
            builder.ClearPendingActions(); Present();
        }
        public void ConfirmReturn()
        {
            if (!isActiveAndEnabled || !paused || !confirmingReturn || !UiReady) return;
            if (!Application.CanStreamedLevelBeLoaded(selectionScenePath)) { returnStatus.text = returnUnavailableMessage; return; }
            returning = true; Present();
            SceneManager.LoadSceneAsync(selectionScenePath, LoadSceneMode.Single);
        }
        private void Unbind()
        {
            if (resume != null) resume.clicked -= Resume;
            if (returnSelection != null) returnSelection.clicked -= RequestReturn;
            if (cancelReturn != null) cancelReturn.clicked -= CancelReturn;
            if (confirmReturn != null) confirmReturn.clicked -= ConfirmReturn;
        }
        private void OnApplicationFocus(bool hasFocus) { if (hardwareInput) ProcessFocus(hasFocus); }
        private void OnApplicationPause(bool applicationPaused) { if (hardwareInput) ProcessFocus(!applicationPaused && Application.isFocused); }
        public void UseValidationInput()
        {
            if (!Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), "-paxSmoke") < 0)
                throw new InvalidOperationException("Input override requires Editor or opt-in validation.");
            hardwareInput = false; focused = true; Resume(); escapeWasHeld = false;
        }
        private void OnDisable()
        {
            Unbind();
            ReleaseClock(); paused = awaitingRelease = false;
            confirmingReturn = returning = uiAwaitingRelease = false;
            if (player != null) player.ClearCommand();
            if (builder != null) builder.ClearPendingActions();
            Present();
        }
    }
}
