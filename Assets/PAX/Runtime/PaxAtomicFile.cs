using System;
using System.IO;
using System.Text;

namespace Pax
{
    // Shared by tile-only and finite snapshots. Validation callbacks must never mutate gameplay.
    public static class PaxAtomicFile
    {
        public static string Read(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("No saved snapshot exists yet.", path);
            if (new FileInfo(path).Length > PaxSaveFile.MaximumBytes) throw new InvalidDataException("Snapshot exceeds the playtest size limit.");
            return File.ReadAllText(path, Encoding.UTF8);
        }
        public static void Write(string path, string content, Func<string, bool> compatible)
        {
            if (Encoding.UTF8.GetByteCount(content) > PaxSaveFile.MaximumBytes || compatible == null || !compatible(content))
                throw new InvalidDataException("Snapshot is too large or incompatible; existing files kept.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                { writer.Write(content); writer.Flush(); file.Flush(true); }
                if (!File.Exists(path)) File.Move(temporary, path);
                else
                {
                    bool valid;
                    try { valid = compatible(Read(path)); }
                    catch (Exception error) when (PaxSaveFile.IsExpectedFailure(error)) { valid = false; }
                    File.Replace(temporary, path, valid ? path + ".bak" : path + ".invalid-" + Guid.NewGuid().ToString("N"));
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
