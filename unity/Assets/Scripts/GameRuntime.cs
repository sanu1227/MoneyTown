using System.Collections.Generic;
using UnityEngine;

namespace MoneyTown
{
    // The development-player verification path never reads or writes the user's PlayerPrefs or save.
    public static class GameRuntime
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static readonly string VerificationDirectory = System.Environment.GetEnvironmentVariable("MONEYTOWN_VERIFY_DIR");
#else
        public static readonly string VerificationDirectory = null;
#endif
        static readonly Dictionary<string, int> verificationPreferences = new Dictionary<string, int>();
        public static bool Verifying => !string.IsNullOrEmpty(VerificationDirectory);
        public static string DataDirectory => Verifying ? System.IO.Path.GetFullPath(VerificationDirectory) : Application.persistentDataPath;
        public static int GetInt(string key, int fallback = 0) => Verifying ? (verificationPreferences.TryGetValue(key, out var value) ? value : fallback) : PlayerPrefs.GetInt(key, fallback);
        public static bool HasKey(string key) => Verifying ? verificationPreferences.ContainsKey(key) : PlayerPrefs.HasKey(key);
        public static void SetInt(string key, int value) { if (Verifying) verificationPreferences[key] = value; else PlayerPrefs.SetInt(key, value); }
        public static void DeleteKey(string key) { if (Verifying) verificationPreferences.Remove(key); else PlayerPrefs.DeleteKey(key); }
        public static void SavePreferences() { if (!Verifying) PlayerPrefs.Save(); }
    }
}
