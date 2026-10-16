using System;
using System.IO;
using UnityEngine;

namespace Pax
{
    public sealed class PaxPlaytestSave : MonoBehaviour
    {
        [SerializeField] private PaxWorld world;
        [SerializeField] private PaxPlayer player;
        [SerializeField] private string saveFolder = "PAXPlaytests";
        [SerializeField] private string saveFile = "pax-playtest.xml";
        public string Status { get; private set; } = "F5 Save edits · F9 Load · F8 Previous save";
        public string SavePath => ResolvePath(Application.persistentDataPath);
        public string BackupPath => SavePath + ".bak";
        private string testDirectory;
        private PaxResourceSession resourceSession;
        public void UseResourceSession(PaxResourceSession session)
        { resourceSession = session; Status = "Finite mode: F5 saves stockpile + tiles; F9 loads; F8 previous snapshot."; }

        public void Configure(PaxWorld grid, PaxPlayer motor) { world = grid; player = motor; }

        // Tests use a unique disposable directory, never the player's real playtest slot.
        public void UseValidationDirectory(string directory)
        {
            if (!Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), "-paxSmoke") < 0)
                throw new InvalidOperationException("Storage override requires an Editor or explicit validation run.");
            testDirectory = Path.GetFullPath(directory);
        }

        private string ResolvePath(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(saveFolder) || string.IsNullOrWhiteSpace(saveFile) ||
                saveFolder != Path.GetFileName(saveFolder) || saveFile != Path.GetFileName(saveFile) ||
                saveFolder == "." || saveFolder == ".." || saveFile == "." || saveFile == ".." ||
                saveFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || saveFile.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Save folder and filename must be simple local names.");
            return Path.Combine(testDirectory ?? Path.Combine(baseDirectory, saveFolder), saveFile);
        }

        public bool SaveEdits()
        {
            if (resourceSession != null) { bool saved = resourceSession.Save(out string status); Status = status; return saved; }
            try
            {
                PaxSaveFile.Write(SavePath, world.CaptureEdits(), previous => world.ValidateEdits(previous, out _));
                Status = "Edits saved. F9 reloads this save; F8 loads the previous save.";
                return true;
            }
            catch (Exception error) when (PaxSaveFile.IsExpectedFailure(error))
            { Status = "Save failed; previous files kept. " + error.Message; return false; }
        }

        public bool LoadEdits(bool previous = false)
        {
            if (resourceSession != null) { bool loaded = resourceSession.Load(previous, out string status); Status = status; return loaded; }
            try
            {
                var data = PaxSaveFile.Read(previous ? BackupPath : SavePath);
                if (!world.TryApplyEdits(data, player.BodyBounds, out string reason))
                { Status = "Load rejected; current world kept. " + reason; return false; }
                Status = previous ? "Previous save loaded. F5 saves these recovered edits." : "Saved tile edits loaded.";
                return true;
            }
            catch (Exception error) when (PaxSaveFile.IsExpectedFailure(error))
            { Status = "Load failed; current world kept. " + error.Message + (previous ? "" : " Try F8 for the previous save."); return false; }
        }
    }
}
