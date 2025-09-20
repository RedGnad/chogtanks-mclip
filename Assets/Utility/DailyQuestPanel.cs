using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Sample
{
    public class DailyQuestPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text questListText;
        [SerializeField] private TMP_Text resetCountdownText;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private int rewardScoreOver15 = 5;       // UI only; keep in sync with server env
        [SerializeField] private int rewardThreeLongMatches = 15;  // UI only; keep in sync with server env

        public void Toggle()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
            if (panelRoot.activeSelf)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            var dqm = DailyQuestManager.Instance;
            if (dqm == null) return;
            var p = dqm.GetProgress();

            // UI: only show quests validated server-side
            bool q2 = p.scoreOver15Claimed; // Score ≥ 5
            bool q3 = p.longMatches >= 3;   // 3 matches ≥ 90s

            if (questListText)
            {
                // No checkboxes; reward turns green when completed
                string pts1 = q2 ? $"<color=#4CAF50>(+{rewardScoreOver15} pts)</color>" : $"<color=#9E9E9E>(+{rewardScoreOver15} pts)</color>";
                string pts2 = q3 ? $"<color=#4CAF50>(+{rewardThreeLongMatches} pts)</color>" : $"<color=#9E9E9E>(+{rewardThreeLongMatches} pts)</color>";
                string line1 = $"Score ≥ 5 {pts1}";
                string line2 = $"Play 3 matches ≥ 90s {pts2}  <color=#BDBDBD>({p.longMatches}/3)</color>";
                questListText.text = line1 + "\n" + line2;
            }

            if (resetCountdownText)
            {
                var now = System.DateTime.UtcNow;
                var delta = p.nextResetUtc - now;
                if (delta.TotalSeconds < 0) delta = System.TimeSpan.Zero;
                resetCountdownText.text = $"Resets in: {delta.Hours:D2}:{delta.Minutes:D2}:{delta.Seconds:D2} ";
            }

            if (headerText)
            {
                headerText.text = "Daily Quests";
            }
        }

        private void Update()
        {
            if (panelRoot != null && panelRoot.activeSelf)
            {
                Refresh();
            }
        }
    }
}


