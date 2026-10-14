using System;
using System.IO;
using System.Linq;

namespace MoneyTown
{
    public static class StorageChecks
    {
        static void Require(bool condition, string message)
        { if (!condition) throw new Exception(message); }
        static void Throws(Action action)
        {
            try { action(); } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException) { return; }
            throw new Exception("Invalid storage operation was accepted.");
        }
        static void Same(byte[] expected, string path)
        { Require(expected.SequenceEqual(File.ReadAllBytes(path)), "Storage operation changed " + Path.GetFileName(path)); }
        static void Snapshot(string source, string operation, byte[] expected)
        {
            var copies = Directory.GetFiles(Path.GetDirectoryName(source), Path.GetFileName(source) + ".before-" + operation + "-*");
            Require(copies.Length > 0 && copies.Any(p => File.ReadAllBytes(p).SequenceEqual(expected)), "Original storage file was not preserved.");
        }
        static void WithStore(Action<GameStore, string> check)
        {
            string directory = Path.Combine(Path.GetTempPath(), "moneytown-storage-check-" + Guid.NewGuid().ToString("N"));
            try { check(new GameStore(directory), Path.Combine(directory, "game-unity-v1.bin")); }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        public static int Run()
        {
            var saved = Market.NextDay(Market.Deposit(Market.Create(42)));
            byte[] good = SaveCodec.Encode(saved), damaged = { 1, 2, 3 };

            // Reset after fallback loading must survive subsequent ordinary saves.
            WithStore((store, path) => {
                File.WriteAllBytes(path + ".backup", good);
                Require(store.Load().Day == saved.Day, "Backup-only loading failed.");
                store.Reset(); store.Save(Market.Create());
                Snapshot(path + ".backup", "reset", good);
            });
            WithStore((store, path) => {
                File.WriteAllBytes(path, damaged); File.WriteAllBytes(path + ".backup", good);
                store.Reset(); store.Save(Market.Create());
                Snapshot(path, "reset", damaged); Snapshot(path + ".backup", "reset", good);
            });
            WithStore((store, path) => {
                File.WriteAllBytes(path, damaged); File.WriteAllBytes(path + ".backup", good);
                Throws(() => store.Load());
                var preview = store.PreviewBackup();
                Require(preview.Day == saved.Day && Market.Total(preview) == Market.Total(saved), "Backup preview differs from saved progress.");
                Same(damaged, path); Same(good, path + ".backup");
                var restored = store.RestoreBackup();
                Require(good.SequenceEqual(SaveCodec.Encode(restored)), "Recovery lost saved state.");
                Same(good, path); Same(good, path + ".backup");
                Snapshot(path, "recovery", damaged); Snapshot(path + ".backup", "recovery", good);
                Require(good.SequenceEqual(SaveCodec.Encode(store.Load())), "Recovered state did not reload.");
                store.RestoreBackup();
                Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.before-recovery-*").Length == 4, "Repeated recovery overwrote a preserved copy.");
            });
            WithStore((store, path) => {
                File.WriteAllBytes(path + ".backup", good);
                store.RestoreBackup();
                Same(good, path); Same(good, path + ".backup");
                Snapshot(path + ".backup", "recovery", good);
            });
            WithStore((store, path) => {
                File.WriteAllBytes(path, damaged);
                Require(store.PreviewBackup() == null, "Missing backup was advertised.");
                Throws(() => store.RestoreBackup()); Same(damaged, path);
                foreach (var invalid in new[] { damaged, new byte[SaveCodec.MaximumBytes+1] }) {
                    File.WriteAllBytes(path + ".backup", invalid);
                    Throws(() => store.PreviewBackup()); Throws(() => store.RestoreBackup());
                    Same(damaged, path); Same(invalid, path + ".backup");
                }
                Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.before-recovery-*").Length == 0, "Invalid recovery wrote preservation files.");
            });
            WithStore((store, path) => {
                File.WriteAllBytes(path, damaged); File.WriteAllBytes(path + ".backup", good);
                Directory.CreateDirectory(path + ".tmp"); // Force a write failure without touching real saves or permissions.
                Throws(() => store.RestoreBackup()); Same(damaged, path); Same(good, path + ".backup");
                Throws(() => store.Reset()); Same(damaged, path); Same(good, path + ".backup");
            });
            return 6;
        }
    }
}
