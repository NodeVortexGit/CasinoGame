using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Casino.Core.Player;
using UnityEngine;

namespace Casino
{
    /// <summary>
    /// JSON save in persistentDataPath with an HMAC so casual hand-editing of the chip balance is detected.
    /// This only deters tinkering: a real-money economy should eventually be server-authoritative (see README).
    /// </summary>
    public sealed class SaveSystem
    {
        const string FileName = "casino_save.json";
        const string UnderageKey = "casino.agegate.blocked";
        // Not a secret against a determined attacker (it ships in the binary), just tamper detection.
        static readonly byte[] Key = Encoding.UTF8.GetBytes("LuckyAce-save-v1-7f3c9a1e5b2d4c6f");

        readonly string path;
        float dirtyTime = -1f;

        public SaveSystem()
        {
            path = Path.Combine(Application.persistentDataPath, FileName);
        }

        public SaveData Data { get; private set; }
        public bool WasTampered { get; private set; }

        /// <summary>The under-age block survives deleting the save, so it is kept separately.</summary>
        public static bool UnderageBlocked
        {
            get => PlayerPrefs.GetInt(UnderageKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(UnderageKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public SaveData Load()
        {
            Data = null;
            try
            {
                if (File.Exists(path))
                {
                    string[] parts = File.ReadAllText(path).Split(new[] { '\n' }, 2);
                    if (parts.Length == 2 && parts[0] == Sign(parts[1]))
                    {
                        Data = JsonUtility.FromJson<SaveData>(parts[1]);
                    }
                    else
                    {
                        WasTampered = true;
                        Debug.LogWarning("Save file signature mismatch - starting a fresh wallet.");
                        var old = parts.Length == 2 ? JsonUtility.FromJson<SaveData>(parts[1]) : null;
                        Data = SaveData.CreateNew(GameConfig.StartingChips, DateTime.UtcNow);
                        if (old != null && old.profileComplete)
                        {
                            // Keep who the player is; only the economy is reset.
                            Data.profileComplete = true;
                            Data.birthYear = old.birthYear;
                            Data.birthMonth = old.birthMonth;
                            Data.birthDay = old.birthDay;
                            Data.gender = old.gender;
                            Data.npcStyle = old.npcStyle;
                            Data.handsPlayed = old.handsPlayed;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Could not read save: " + e.Message);
            }
            if (Data == null) Data = SaveData.CreateNew(GameConfig.StartingChips, DateTime.UtcNow);
            if (Data.grantedTransactions == null) Data.grantedTransactions = new System.Collections.Generic.List<string>();
            return Data;
        }

        public void MarkDirty()
        {
            if (dirtyTime < 0f) dirtyTime = Time.unscaledTime;
        }

        /// <summary>Call every frame; writes at most every 2 seconds.</summary>
        public void Tick()
        {
            if (dirtyTime >= 0f && Time.unscaledTime - dirtyTime > 2f) SaveNow();
        }

        public void SaveNow()
        {
            dirtyTime = -1f;
            if (Data == null) return;
            try
            {
                string json = JsonUtility.ToJson(Data);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, Sign(json) + "\n" + json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not write save: " + e.Message);
            }
        }

        public void DeleteAll()
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
            Data = SaveData.CreateNew(GameConfig.StartingChips, DateTime.UtcNow);
        }

        static string Sign(string json)
        {
            using (var h = new HMACSHA256(Key))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(json)));
        }
    }
}
