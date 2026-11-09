using UnityEngine;
using UnityEngine.Serialization;

namespace Demo2.TableTennis
{
    public enum TrainerMode { NormalPractice, AIOpponent }

    /// <summary>
    /// High-level trainer coordinator for mode switching and rally outcomes.
    /// It wires ServeController and RallyScorekeeper together and routes active-ball events into score logic.
    /// </summary>
    [RequireComponent(typeof(TableCourt), typeof(ServeController), typeof(RallyScorekeeper))]
    public sealed class VRTableTennisTrainer : MonoBehaviour
    {
        // Section: Inspector configuration.
        [Header("Scene References")]
        [SerializeField] ServeController serveController;
        [SerializeField] RallyScorekeeper scorekeeper;

        [Header("Mode")]
        [SerializeField] TrainerMode mode = TrainerMode.NormalPractice;

        [Header("Mode Buttons")]
        [SerializeField] PaddleButton resetButton;
        [FormerlySerializedAs("volleyPracticeButton")]
        [SerializeField] PaddleButton normalPracticeButton;
        [SerializeField] PaddleButton aiOpponentButton;

        // Section: Public trainer state.
        public int Score => scorekeeper != null ? scorekeeper.Score : 0;
        public int Bonuses => scorekeeper != null ? scorekeeper.Bonuses : 0;
        public int Attempts => scorekeeper != null ? scorekeeper.Attempts : 0;
        public int Misses => scorekeeper != null ? scorekeeper.Misses : 0;
        public int HomeScore => scorekeeper != null ? scorekeeper.HomeScore : 0;
        public int AwayScore => scorekeeper != null ? scorekeeper.AwayScore : 0;
        public int Volleys => scorekeeper != null ? scorekeeper.Volleys : 0;
        public TrainerMode Mode => mode;
        public TableCourt Court => serveController != null ? serveController.Court : null;
        public event System.Action<TrainerMode> ModeChanged;

        // Section: Lifecycle and button event wiring.
        void OnEnable()
        {
            if (resetButton != null) resetButton.Pressed += HandleResetPressed;
            if (normalPracticeButton != null) normalPracticeButton.Pressed += HandleNormalPracticePressed;
            if (aiOpponentButton != null) aiOpponentButton.Pressed += HandleAiOpponentPressed;
        }

        void OnDisable()
        {
            if (resetButton != null) resetButton.Pressed -= HandleResetPressed;
            if (normalPracticeButton != null) normalPracticeButton.Pressed -= HandleNormalPracticePressed;
            if (aiOpponentButton != null) aiOpponentButton.Pressed -= HandleAiOpponentPressed;
            if (serveController != null) serveController.StopServing();
        }

        // Engine setup and initial scene validation.
        void Awake()
        {
            if (serveController == null) serveController = GetComponent<ServeController>();
            if (scorekeeper == null) scorekeeper = GetComponent<RallyScorekeeper>();

            Physics.defaultSolverIterations = Mathf.Max(Physics.defaultSolverIterations, 12);
            Physics.defaultSolverVelocityIterations = Mathf.Max(Physics.defaultSolverVelocityIterations, 4);
            Time.fixedDeltaTime = 1f / 72f; //Match the physics timestep to the headset refresh rate for more consistent ball simulation.
        }

        void Start()
        {
            if (!ValidateReferences()) { enabled = false; return; }
            serveController.Initialize(this);
            scorekeeper.Initialize(mode);
            serveController.BeginServing();
        }

        void HandleResetPressed(PaddleButton _) => ResetTrainer();

        void HandleNormalPracticePressed(PaddleButton _) => SetMode(TrainerMode.NormalPractice);

        void HandleAiOpponentPressed(PaddleButton _) => SetMode(TrainerMode.AIOpponent);

        // Section: Public mode and reset controls.
        public void SetMode(TrainerMode newMode)
        {
            if (mode == newMode) return;

            mode = newMode;
            Debug.Log($"Trainer mode changed to {mode}.", this);
            ModeChanged?.Invoke(mode);
            ResetTrainer();
        }

        public void ResetTrainer()
        {
            if (scorekeeper != null) scorekeeper.ResetScores(mode);
            int removedBallCount = serveController != null ? serveController.ResetAndRestart() : 0;
            Debug.Log($"Trainer reset; removed {removedBallCount} ball(s).", this);
        }

        // Section: Internal reference validation.
        bool ValidateReferences()
        {
            bool valid = true;
            if (serveController == null)
            {
                Debug.LogError("Assign Serve Controller on VRTableTennisTrainer.", this);
                valid = false;
            }
            else if (!serveController.ValidateReferences()) valid = false;

            if (scorekeeper == null)
            {
                Debug.LogError("Assign Rally Scorekeeper on VRTableTennisTrainer.", this);
                valid = false;
            }
            else if (!scorekeeper.ValidateReferences()) valid = false;
            return valid;
        }

        // Section: Public rally outcome API.
        // Both tracked and AI rallies resolve through the same side-neutral point path.
        public void RegisterPoint(TrainingBall ball, TableSide winner, bool earnedTargetBonus)
        {
            if (!IsActiveBall(ball) || ball.Scored || ball.State == TrainingBallState.Faulted || winner == TableSide.None) return;
            ball.MarkScored();
            ReleaseActiveBall(ball);
            scorekeeper.RegisterPoint(winner, earnedTargetBonus);
        }

        // Kept for existing callers and Inspector events.
        public void RegisterScore(TrainingBall ball, bool earnedTargetBonus)
        {
            RegisterPoint(ball, TableSide.Player, earnedTargetBonus);
        }

        // The first legal contact starts the attempt count for the current serve.
        public void NotifyServeInPlay(TrainingBall ball)
        {
            if (!IsActiveBall(ball) || !ball.CountsTowardServe || ball.State == TrainingBallState.Faulted) return;
            scorekeeper.RegisterAttempt();
        }

        public void RegisterFoul(TrainingBall ball, TrainingFoul foul)
        {
            if (!IsActiveBall(ball)) return;

            if (foul == TrainingFoul.Player)
                scorekeeper.RegisterPoint(TableSide.Opponent, false);
            else if (ball.CountsTowardServe)
                scorekeeper.RegisterPoint(TableSide.Player, ball.EarnedTargetBonus);
            else
                scorekeeper.PlayServeFoul(); // Bad machine serve: replay without changing stats.

            ReleaseActiveBall(ball);
        }

        public void Retire(TrainingBall ball)
        {
            if (IsActiveBall(ball) && !ball.Scored && ball.State != TrainingBallState.Faulted)
                scorekeeper.RegisterRetiredMiss();

            ReleaseActiveBall(ball);
            ball.MarkRetired();
        }

        // Section: Active-ball helpers.
        void ReleaseActiveBall(TrainingBall ball)
        {
            if (serveController != null) serveController.ReleaseBall(ball);
        }

        bool IsActiveBall(TrainingBall ball) =>
            serveController != null && serveController.IsActiveBall(ball);
    }
}
