using TMPro;
using UnityEngine;

namespace Demo2.TableTennis
{
    /// <summary>
    /// Maintains score-state counters and updates the in-world scoreboard display.
    /// Also handles success, miss, and foul feedback audio after rally outcomes are resolved.
    /// </summary>
    [RequireComponent(typeof(ServeController))]
    public sealed class RallyScorekeeper : MonoBehaviour
    {
        // Section: Inspector references.
        [Header("Scene References")]
        [SerializeField] ServeController serveController;

        [Header("Scoreboard Values")]
        [SerializeField] TextMeshPro scoreText;
        [SerializeField] TextMeshPro missesText;
        [SerializeField] TextMeshPro bonusText;
        [SerializeField] TextMeshPro serveNumberText;

        [Header("Scoreboard Labels")]
        [Tooltip("Displays SCORE in practice and HOME in AI opponent mode.")]
        [SerializeField] TextMeshPro scoreLabelText;
        [Tooltip("Displays MISSES in practice and AWAY in AI opponent mode.")]
        [SerializeField] TextMeshPro missesLabelText;

        [Header("Feedback Audio")]
        [SerializeField] AudioSource feedbackAudioSource;
        [SerializeField] AudioClip foulBuzzer;
        [SerializeField] AudioClip failBuzzer;
        [SerializeField] AudioClip successDing;
        [Tooltip("Played instead of Success Ding when a valid score also hits the bonus target.")]
        [SerializeField] AudioClip bonusSuccessDing;

        // Section: Public counters.
        public int Score { get; private set; }
        public int Misses { get; private set; }
        public int Bonuses { get; private set; }
        public int Attempts { get; private set; }
        public int HomeScore => Score;
        public int AwayScore => Misses;
        public int Volleys => Attempts;

        // Section: Runtime mode state.
        TrainerMode mode;

        // Section: Lifecycle.
        void Awake()
        {
            if (serveController == null) serveController = GetComponent<ServeController>();
            ConfigureAudio();
        }

        // Section: Public API.
        public bool ValidateReferences()
        {
            if (serveController != null) return true;
            Debug.LogError("Assign Serve Controller on RallyScorekeeper.", this);
            return false;
        }

        public void Initialize(TrainerMode initialMode)
        {
            mode = initialMode;
            RefreshDisplay();
        }

        public void ResetScores(TrainerMode newMode)
        {
            mode = newMode;
            if (feedbackAudioSource != null) feedbackAudioSource.Stop();
            Score = 0;
            Misses = 0;
            Bonuses = 0;
            Attempts = 0;
            RefreshDisplay();
        }

        public void RegisterPoint(TableSide winner, bool earnedTargetBonus)
        {
            if (winner == TableSide.Player)
            {
                Score++;
                if (earnedTargetBonus) Bonuses++;
                AudioClip clip = earnedTargetBonus && bonusSuccessDing != null
                    ? bonusSuccessDing
                    : successDing;
                PlayFeedback(clip);
            }
            else if (winner == TableSide.Opponent)
            {
                Misses++;
                PlayFeedback(failBuzzer);
            }
            else
            {
                return;
            }

            serveController.MoveTargetZone();
            RefreshDisplay();
            Debug.Log(
                $"Point resolved: winner={winner}, score={Score}, misses={Misses}, bonuses={Bonuses}.",
                this);
        }

        public void RegisterAttempt()
        {
            Attempts++;
            RefreshDisplay();
        }

        public void RegisterRetiredMiss()
        {
            Misses++;
            serveController.MoveTargetZone();
            RefreshDisplay();
            PlayFeedback(failBuzzer);
        }

        public void PlayServeFoul()
        {
            PlayFeedback(foulBuzzer);
        }

        // Section: Display and audio helpers.
        void RefreshDisplay()
        {
            if (scoreText != null) scoreText.text = Score.ToString();
            if (missesText != null) missesText.text = Misses.ToString();
            if (bonusText != null) bonusText.text = Bonuses.ToString();
            if (serveNumberText != null)
                serveNumberText.text = serveController != null
                    ? serveController.GetServeNumber(Attempts).ToString()
                    : "0";

            bool aiMode = mode == TrainerMode.AIOpponent;
            if (scoreLabelText != null) scoreLabelText.text = aiMode ? "HOME" : "SCORE";
            if (missesLabelText != null) missesLabelText.text = aiMode ? "AWAY" : "MISSES";
        }

        void ConfigureAudio()
        {
            if (feedbackAudioSource == null) return;
            feedbackAudioSource.spatialBlend = 1f;
            feedbackAudioSource.spatialize = true;
            feedbackAudioSource.playOnAwake = false;
        }

        void PlayFeedback(AudioClip clip)
        {
            if (feedbackAudioSource != null && clip != null)
                feedbackAudioSource.PlayOneShot(clip);
        }
    }
}
