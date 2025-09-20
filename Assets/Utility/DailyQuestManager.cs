using UnityEngine;

namespace Sample
{
    [DefaultExecutionOrder(-200)]
    public class DailyQuestManager : MonoBehaviour
    {
        public static DailyQuestManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public struct QuestProgress
        {
            public string dayKey;
            public int longMatches;          // >= 90s
            public bool scoreOver15Claimed;  // true si déjà accompli aujourd'hui
            public int enemyKills;
            public int realPlayerKills;
            public int specialShots;
            public System.DateTime nextResetUtc;
        }

        private string GetDayKey()
        {
            return System.DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        private void EnsureDay()
        {
            string currentKey = GetDayKey();
            string savedKey = PlayerPrefs.GetString("DQM.day", "");
            if (savedKey != currentKey)
            {
                PlayerPrefs.SetString("DQM.day", currentKey);
                PlayerPrefs.SetInt("DQM.longMatches", 0);
                PlayerPrefs.SetInt("DQM.enemyKills", 0);
                PlayerPrefs.SetInt("DQM.realPlayerKills", 0);
                PlayerPrefs.SetInt("DQM.specialShots", 0);
                PlayerPrefs.SetInt("DQM.score15", 0);
                PlayerPrefs.Save();
            }
        }

        public void RecordMatchEnd(int localScore, float durationSeconds)
        {
            EnsureDay();
            if (durationSeconds >= 90f)
            {
                int cur = PlayerPrefs.GetInt("DQM.longMatches", 0);
                PlayerPrefs.SetInt("DQM.longMatches", cur + 1);
            }
            if (localScore >= 5)
            {
                PlayerPrefs.SetInt("DQM.score15", 1);
            }
            PlayerPrefs.Save();
        }

        // UI-only: count a 90s milestone even if match doesn’t end (avoid double count by caller)
        public void RecordLongMatch()
        {
            EnsureDay();
            int cur = PlayerPrefs.GetInt("DQM.longMatches", 0);
            PlayerPrefs.SetInt("DQM.longMatches", cur + 1);
            PlayerPrefs.Save();
        }

        public void RecordEnemyKill()
        {
            EnsureDay();
            int cur = PlayerPrefs.GetInt("DQM.enemyKills", 0);
            PlayerPrefs.SetInt("DQM.enemyKills", cur + 1);
            PlayerPrefs.Save();
        }

        public void RecordRealPlayerKill()
        {
            EnsureDay();
            int cur = PlayerPrefs.GetInt("DQM.realPlayerKills", 0);
            PlayerPrefs.SetInt("DQM.realPlayerKills", cur + 1);
            PlayerPrefs.Save();
        }

        public void RecordSpecialShot()
        {
            EnsureDay();
            int cur = PlayerPrefs.GetInt("DQM.specialShots", 0);
            PlayerPrefs.SetInt("DQM.specialShots", cur + 1);
            PlayerPrefs.Save();
        }

        public QuestProgress GetProgress()
        {
            EnsureDay();
            var utcNow = System.DateTime.UtcNow;
            var nextReset = new System.DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, System.DateTimeKind.Utc).AddDays(1);
            return new QuestProgress
            {
                dayKey = PlayerPrefs.GetString("DQM.day", GetDayKey()),
                longMatches = PlayerPrefs.GetInt("DQM.longMatches", 0),
                scoreOver15Claimed = PlayerPrefs.GetInt("DQM.score15", 0) == 1,
                enemyKills = PlayerPrefs.GetInt("DQM.enemyKills", 0),
                realPlayerKills = PlayerPrefs.GetInt("DQM.realPlayerKills", 0),
                specialShots = PlayerPrefs.GetInt("DQM.specialShots", 0),
                nextResetUtc = nextReset
            };
        }
    }
}


