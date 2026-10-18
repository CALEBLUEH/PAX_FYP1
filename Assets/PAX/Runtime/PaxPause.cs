using System;
using UnityEngine;
using UnityEngine.UIElements;

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
        private VisualElement overlay;
        private Label title;
        private Button resume;
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
        private void Start() { started = true; BindView(); }
        private void BindView()
        {
            if (resume != null) resume.clicked -= Resume;
            overlay = document.rootVisualElement.Q("pause-overlay");
            title = document.rootVisualElement.Q<Label>("pause-title");
            resume = document.rootVisualElement.Q<Button>("pause-resume");
            document.rootVisualElement.Q<Label>("pause-mission").text = missionHelp;
            resume.clicked += Resume;
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
            if (pressed) { if (paused) Resume(); else Pause(); }
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
            if (!isActiveAndEnabled || !paused || !focused) return;
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
            resume.SetEnabled(focused);
        }
        public void ProcessFocus(bool hasFocus)
        {
            if (!isActiveAndEnabled) return;
            focused = hasFocus;
            // Regaining focus never resumes or interprets an already-held Escape as a fresh press.
            escapeWasHeld = true;
            if (!focused) Pause();
            Present();
            if (focused && paused) resume?.Focus();
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
            if (resume != null) resume.clicked -= Resume;
            ReleaseClock(); paused = awaitingRelease = false;
            if (player != null) player.ClearCommand();
            if (builder != null) builder.ClearPendingActions();
            Present();
        }
    }
}
