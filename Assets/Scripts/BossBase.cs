using UnityEngine;

public class BossBase : EnemyBase
{
    public enum BossBehaviorState
    {
        Inactive,
        Intro,
        Idle,
        Attack,
        JumpAttack,
        Recovery,
        SummonHelpers,
        PhaseTransition,
        Stunned,
        Dead
    }

    public enum BossPhaseSelectionMode
    {
        OrderedLoop,
        Random
    }

    [System.Serializable]
    public class BossStateStep
    {
        [Tooltip("Behavior state entered for this step.")]
        public BossBehaviorState state = BossBehaviorState.Attack;

        [Tooltip("How long the boss remains in this state. Set to 0 to wait until CompleteCurrentState is called.")]
        [Min(0f)]
        public float duration = 1f;

        [Tooltip("Face the player when this state begins.")]
        public bool facePlayerOnEnter = true;

        public BossStateStep()
        {
        }

        public BossStateStep(BossBehaviorState state, float duration, bool facePlayerOnEnter = true)
        {
            this.state = state;
            this.duration = duration;
            this.facePlayerOnEnter = facePlayerOnEnter;
        }
    }

    [System.Serializable]
    public class BossPhase
    {
        [Tooltip("Designer-facing name for this phase.")]
        public string phaseName = "Phase";

        [Tooltip("This phase starts when current HP percent is at or below this value.")]
        [Range(0f, 1f)]
        public float enterAtHealthPercent = 1f;

        [Tooltip("How this phase chooses its next behavior state.")]
        public BossPhaseSelectionMode selectionMode = BossPhaseSelectionMode.OrderedLoop;

        [Tooltip("When using Random selection, avoid picking the same step twice in a row when possible.")]
        public bool avoidRandomRepeats = true;

        [Tooltip("Behavior states used by this phase.")]
        public BossStateStep[] behaviorLoop =
        {
            new BossStateStep(BossBehaviorState.Attack, 1f),
            new BossStateStep(BossBehaviorState.Recovery, 1f, false)
        };

        public BossPhase()
        {
        }

        public BossPhase(
            string phaseName,
            float enterAtHealthPercent,
            BossPhaseSelectionMode selectionMode,
            BossStateStep[] behaviorLoop
        )
        {
            this.phaseName = phaseName;
            this.enterAtHealthPercent = enterAtHealthPercent;
            this.selectionMode = selectionMode;
            this.behaviorLoop = behaviorLoop;
        }
    }

    [Header("Boss Activation")]
    [Tooltip("Start the boss state machine automatically when the scene begins.")]
    [SerializeField] private bool startsActive = true;

    [Tooltip("Delay after activation before the first behavior begins.")]
    [SerializeField] private float activationDelay = 0f;

    [Header("Boss Phases")]
    [Tooltip("Phases are selected from HP percentage. Lower thresholds represent later phases.")]
    [SerializeField] private BossPhase[] phases =
    {
        new BossPhase(
            "Phase 1",
            1f,
            BossPhaseSelectionMode.OrderedLoop,
            new[]
            {
                new BossStateStep(BossBehaviorState.Attack, 1.25f),
                new BossStateStep(BossBehaviorState.Recovery, 0.75f, false),
                new BossStateStep(BossBehaviorState.JumpAttack, 1f),
                new BossStateStep(BossBehaviorState.Recovery, 1f, false)
            }
        ),
        new BossPhase(
            "Phase 2",
            0.65f,
            BossPhaseSelectionMode.OrderedLoop,
            new[]
            {
                new BossStateStep(BossBehaviorState.Attack, 1f),
                new BossStateStep(BossBehaviorState.JumpAttack, 1f),
                new BossStateStep(BossBehaviorState.Recovery, 0.6f, false),
                new BossStateStep(BossBehaviorState.SummonHelpers, 0.75f),
                new BossStateStep(BossBehaviorState.Recovery, 0.8f, false)
            }
        ),
        new BossPhase(
            "Phase 3",
            0.33f,
            BossPhaseSelectionMode.Random,
            new[]
            {
                new BossStateStep(BossBehaviorState.Attack, 0.85f),
                new BossStateStep(BossBehaviorState.JumpAttack, 0.9f),
                new BossStateStep(BossBehaviorState.SummonHelpers, 0.65f),
                new BossStateStep(BossBehaviorState.Recovery, 0.45f, false)
            }
        )
    };

    [Tooltip("Restart the behavior loop when the boss enters a new phase.")]
    [SerializeField] private bool restartBehaviorOnPhaseChange = true;

    [Tooltip("Allow the boss to move back to an earlier phase if it is healed above a phase threshold.")]
    [SerializeField] private bool allowPhaseRegression = false;

    [Header("Boss Targeting")]
    [Tooltip("Optional player target. If empty, the boss searches for JonCharacterController or Hero.")]
    [SerializeField] private Transform playerTargetOverride;

    [Tooltip("How often the boss searches for the player if no target is assigned.")]
    [SerializeField] private float playerSearchInterval = 0.5f;

    [Header("Jump Attack")]
    [Tooltip("Use this generic jump impulse when entering JumpAttack. Override OnJumpAttackStateEntered for custom jumps.")]
    [SerializeField] private bool useDefaultJumpAttackImpulse = true;

    [Tooltip("Impulse applied toward the player when the default JumpAttack begins.")]
    [SerializeField] private Vector2 jumpAttackImpulse = new Vector2(8f, 10f);

    [Tooltip("Clear current velocity before the default JumpAttack impulse.")]
    [SerializeField] private bool resetVelocityBeforeJumpAttack = true;

    [Header("Helper Summons")]
    [Tooltip("Helper prefabs spawned by the default SummonHelpers state.")]
    [SerializeField] private GameObject[] helperPrefabs;

    [Tooltip("Spawn points used by the default SummonHelpers state. If empty, helpers spawn beside the boss.")]
    [SerializeField] private Transform[] helperSpawnPoints;

    [Tooltip("How many helpers are spawned each time SummonHelpers begins.")]
    [SerializeField] private int helpersPerSummon = 2;

    [Tooltip("Fallback offset used when no helper spawn points are assigned.")]
    [SerializeField] private Vector2 fallbackHelperSpawnOffset = new Vector2(2f, 0f);

    [Header("Recovery")]
    [Tooltip("Stop horizontal movement when entering recovery, phase transition, or stun states.")]
    [SerializeField] private bool stopHorizontalVelocityDuringRecovery = true;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = false;

    private Transform playerTransform;
    private float nextPlayerSearchTime = float.NegativeInfinity;
    private float stateStartTime;
    private float stateEndTime = float.PositiveInfinity;
    private float phaseStartTime;
    private int maxHp;
    private int currentPhaseIndex = -1;
    private int currentStateStepIndex = -1;
    private bool isBossActive;
    private bool currentStateCompleted;
    private BossBehaviorState currentState = BossBehaviorState.Inactive;

    public BossBehaviorState CurrentState => currentState;
    public int CurrentPhaseIndex => currentPhaseIndex;
    public BossPhase CurrentPhase => IsValidPhaseIndex(currentPhaseIndex) ? phases[currentPhaseIndex] : null;
    public float CurrentHealthPercent => maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
    public float CurrentStateElapsed => Time.time - stateStartTime;
    public float CurrentPhaseElapsed => Time.time - phaseStartTime;
    public bool IsBossActive => isBossActive;

    protected virtual void Reset()
    {
        moveSpeed = 0f;
    }

    protected override void Awake()
    {
        base.Awake();
        maxHp = Mathf.Max(1, hp);
    }

    protected virtual void Start()
    {
        maxHp = Mathf.Max(Mathf.Max(maxHp, hp), 1);
        EnsurePhaseConfiguration();
        currentPhaseIndex = GetPhaseIndexForHealth();

        if (startsActive)
        {
            ActivateBoss();
        }
        else
        {
            EnterState(BossBehaviorState.Inactive, 0f, false);
        }
    }

    protected override void Update()
    {
        if (isDead)
            return;

        base.Update();

        if (isDead)
            return;

        UpdatePhaseFromHealth();

        if (!isBossActive)
            return;

        UpdateCurrentState(Time.deltaTime);

        if (ShouldAdvanceCurrentState())
        {
            AdvanceState();
        }
    }

    protected override void FixedUpdate()
    {
        if (isDead || !isBossActive)
            return;

        FixedUpdateCurrentState(Time.fixedDeltaTime);
        Move();
    }

    protected override void Move()
    {
    }

    public virtual void ActivateBoss()
    {
        if (isDead || isBossActive)
            return;

        EnsurePhaseConfiguration();
        isBossActive = true;
        currentPhaseIndex = GetPhaseIndexForHealth();
        phaseStartTime = Time.time;
        OnBossActivated();

        if (activationDelay > 0f)
        {
            EnterState(BossBehaviorState.Intro, activationDelay, false);
            return;
        }

        StartPhaseBehavior(currentPhaseIndex);
    }

    public virtual void DeactivateBoss()
    {
        if (!isBossActive)
            return;

        ExitCurrentState();
        isBossActive = false;
        EnterState(BossBehaviorState.Inactive, 0f, false);
        OnBossDeactivated();
    }

    public virtual void Stun(float duration)
    {
        if (isDead || !isBossActive)
            return;

        ExitCurrentState();
        EnterState(BossBehaviorState.Stunned, Mathf.Max(0f, duration), false);
    }

    public override void TakeDamage(int amount)
    {
        bool wasAlive = !isDead;

        base.TakeDamage(amount);

        if (!wasAlive || isDead || amount <= 0)
            return;

        UpdatePhaseFromHealth();
    }

    protected void CompleteCurrentState()
    {
        if (!isBossActive || currentState == BossBehaviorState.Inactive || currentState == BossBehaviorState.Dead)
            return;

        currentStateCompleted = true;
    }

    protected void ForceState(BossBehaviorState nextState, float duration, bool facePlayerOnEnter = true)
    {
        if (isDead || !isBossActive)
            return;

        ExitCurrentState();
        EnterState(nextState, Mathf.Max(0f, duration), facePlayerOnEnter);
    }

    protected void ForceNextState()
    {
        if (!isBossActive || isDead)
            return;

        AdvanceState();
    }

    protected Transform GetPlayerTransform(bool forceSearch = false)
    {
        ResolvePlayerTransform(forceSearch);
        return playerTransform;
    }

    protected bool TryGetDirectionToPlayer(out float direction)
    {
        direction = facingDirection == 0f ? 1f : facingDirection;
        Transform target = GetPlayerTransform();

        if (target == null)
            return false;

        float deltaX = target.position.x - transform.position.x;

        if (Mathf.Approximately(deltaX, 0f))
            return false;

        direction = Mathf.Sign(deltaX);
        return true;
    }

    protected float GetDirectionToPlayerOrFacing()
    {
        if (TryGetDirectionToPlayer(out float direction))
        {
            return direction;
        }

        return facingDirection == 0f ? 1f : Mathf.Sign(facingDirection);
    }

    protected void FacePlayer()
    {
        FaceDirection(GetDirectionToPlayerOrFacing());
    }

    protected void FaceDirection(float direction)
    {
        if (Mathf.Approximately(direction, 0f))
            return;

        float newFacingDirection = Mathf.Sign(direction);

        if (Mathf.Approximately(newFacingDirection, facingDirection))
            return;

        facingDirection = newFacingDirection;
        UpdateSpriteDirection();
    }

    protected virtual void OnBossActivated()
    {
    }

    protected virtual void OnBossDeactivated()
    {
    }

    protected virtual void OnBossPhaseChanged(int previousPhaseIndex, int newPhaseIndex, BossPhase phase)
    {
    }

    protected virtual void OnBossStateEntered(BossBehaviorState state)
    {
    }

    protected virtual void OnBossStateUpdated(BossBehaviorState state, float deltaTime)
    {
    }

    protected virtual void OnBossStateFixedUpdated(BossBehaviorState state, float fixedDeltaTime)
    {
    }

    protected virtual void OnBossStateExited(BossBehaviorState state)
    {
    }

    protected virtual void OnIntroStateEntered()
    {
    }

    protected virtual void OnIntroStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnIntroStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnIntroStateExited()
    {
    }

    protected virtual void OnIdleStateEntered()
    {
    }

    protected virtual void OnIdleStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnIdleStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnIdleStateExited()
    {
    }

    protected virtual void OnAttackStateEntered()
    {
    }

    protected virtual void OnAttackStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnAttackStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnAttackStateExited()
    {
    }

    protected virtual void OnJumpAttackStateEntered()
    {
        if (!useDefaultJumpAttackImpulse || rb2d == null)
            return;

        float jumpDirection = GetDirectionToPlayerOrFacing();

        if (resetVelocityBeforeJumpAttack)
        {
            rb2d.linearVelocity = Vector2.zero;
        }

        rb2d.AddForce(
            new Vector2(jumpDirection * Mathf.Abs(jumpAttackImpulse.x), jumpAttackImpulse.y),
            ForceMode2D.Impulse
        );
    }

    protected virtual void OnJumpAttackStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnJumpAttackStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnJumpAttackStateExited()
    {
    }

    protected virtual void OnRecoveryStateEntered()
    {
        StopHorizontalVelocityForRecovery();
    }

    protected virtual void OnRecoveryStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnRecoveryStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnRecoveryStateExited()
    {
    }

    protected virtual void OnSummonHelpersStateEntered()
    {
        SummonHelpers();
    }

    protected virtual void OnSummonHelpersStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnSummonHelpersStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnSummonHelpersStateExited()
    {
    }

    protected virtual void OnPhaseTransitionStateEntered()
    {
        StopHorizontalVelocityForRecovery();
    }

    protected virtual void OnPhaseTransitionStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnPhaseTransitionStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnPhaseTransitionStateExited()
    {
    }

    protected virtual void OnStunnedStateEntered()
    {
        StopHorizontalVelocityForRecovery();
    }

    protected virtual void OnStunnedStateUpdated(float deltaTime)
    {
    }

    protected virtual void OnStunnedStateFixedUpdated(float fixedDeltaTime)
    {
    }

    protected virtual void OnStunnedStateExited()
    {
    }

    protected virtual void SummonHelpers()
    {
        if (helperPrefabs == null || helperPrefabs.Length == 0 || helpersPerSummon <= 0)
            return;

        for (int i = 0; i < helpersPerSummon; i++)
        {
            GameObject helperPrefab = GetHelperPrefab(i);

            if (helperPrefab == null)
                continue;

            Vector3 spawnPosition = GetHelperSpawnPosition(i);
            Quaternion spawnRotation = helperPrefab.transform.rotation;
            Instantiate(helperPrefab, spawnPosition, spawnRotation);
        }
    }

    protected override void Die()
    {
        if (isDead)
            return;

        ExitCurrentState();
        currentState = BossBehaviorState.Dead;
        isBossActive = false;
        base.Die();
    }

    private void EnsurePhaseConfiguration()
    {
        if (phases != null && phases.Length > 0)
            return;

        phases = new[]
        {
            new BossPhase(
                "Phase 1",
                1f,
                BossPhaseSelectionMode.OrderedLoop,
                new[]
                {
                    new BossStateStep(BossBehaviorState.Attack, 1f),
                    new BossStateStep(BossBehaviorState.Recovery, 1f, false)
                }
            )
        };
    }

    private void UpdatePhaseFromHealth()
    {
        if (!isBossActive || phases == null || phases.Length == 0)
            return;

        int nextPhaseIndex = GetPhaseIndexForHealth();

        if (nextPhaseIndex == currentPhaseIndex)
            return;

        if (!allowPhaseRegression && IsPhaseRegression(nextPhaseIndex))
            return;

        int previousPhaseIndex = currentPhaseIndex;
        currentPhaseIndex = nextPhaseIndex;
        phaseStartTime = Time.time;
        OnBossPhaseChanged(previousPhaseIndex, currentPhaseIndex, CurrentPhase);

        if (logStateChanges)
        {
            Debug.Log($"{gameObject.name} entered boss phase {GetCurrentPhaseName()}.");
        }

        if (restartBehaviorOnPhaseChange)
        {
            ExitCurrentState();
            StartPhaseBehavior(currentPhaseIndex);
        }
    }

    private void StartPhaseBehavior(int phaseIndex)
    {
        if (!IsValidPhaseIndex(phaseIndex))
        {
            EnterState(BossBehaviorState.Idle, 0f, false);
            return;
        }

        currentStateStepIndex = GetFirstStepIndex(phases[phaseIndex]);
        EnterCurrentStep();
    }

    private void AdvanceState()
    {
        ExitCurrentState();

        if (currentState == BossBehaviorState.Intro)
        {
            StartPhaseBehavior(currentPhaseIndex);
            return;
        }

        BossPhase phase = CurrentPhase;

        if (phase == null || phase.behaviorLoop == null || phase.behaviorLoop.Length == 0)
        {
            EnterState(BossBehaviorState.Idle, 0f, false);
            return;
        }

        currentStateStepIndex = GetNextStepIndex(phase, currentStateStepIndex);
        EnterCurrentStep();
    }

    private void EnterCurrentStep()
    {
        BossStateStep step = GetCurrentStep();

        if (step == null)
        {
            EnterState(BossBehaviorState.Idle, 0f, false);
            return;
        }

        EnterState(step.state, step.duration, step.facePlayerOnEnter);
    }

    private void EnterState(BossBehaviorState nextState, float duration, bool facePlayerOnEnter)
    {
        currentState = nextState;
        currentStateCompleted = false;
        stateStartTime = Time.time;
        stateEndTime = duration > 0f ? Time.time + duration : float.PositiveInfinity;

        if (facePlayerOnEnter)
        {
            FacePlayer();
        }

        if (logStateChanges)
        {
            Debug.Log($"{gameObject.name} boss state: {currentState} ({GetCurrentPhaseName()}).");
        }

        OnBossStateEntered(currentState);
        DispatchStateEntered(currentState);
    }

    private void UpdateCurrentState(float deltaTime)
    {
        OnBossStateUpdated(currentState, deltaTime);
        DispatchStateUpdated(currentState, deltaTime);
    }

    private void FixedUpdateCurrentState(float fixedDeltaTime)
    {
        OnBossStateFixedUpdated(currentState, fixedDeltaTime);
        DispatchStateFixedUpdated(currentState, fixedDeltaTime);
    }

    private void ExitCurrentState()
    {
        if (currentState == BossBehaviorState.Inactive || currentState == BossBehaviorState.Dead)
            return;

        DispatchStateExited(currentState);
        OnBossStateExited(currentState);
    }

    private bool ShouldAdvanceCurrentState()
    {
        if (currentState == BossBehaviorState.Inactive || currentState == BossBehaviorState.Dead)
            return false;

        return currentStateCompleted || Time.time >= stateEndTime;
    }

    private BossStateStep GetCurrentStep()
    {
        BossPhase phase = CurrentPhase;

        if (phase == null || phase.behaviorLoop == null)
            return null;

        if (currentStateStepIndex < 0 || currentStateStepIndex >= phase.behaviorLoop.Length)
            return null;

        return phase.behaviorLoop[currentStateStepIndex];
    }

    private int GetFirstStepIndex(BossPhase phase)
    {
        if (phase == null || phase.behaviorLoop == null || phase.behaviorLoop.Length == 0)
            return -1;

        if (phase.selectionMode == BossPhaseSelectionMode.Random)
        {
            return UnityEngine.Random.Range(0, phase.behaviorLoop.Length);
        }

        return 0;
    }

    private int GetNextStepIndex(BossPhase phase, int previousStepIndex)
    {
        if (phase == null || phase.behaviorLoop == null || phase.behaviorLoop.Length == 0)
            return -1;

        if (phase.selectionMode == BossPhaseSelectionMode.Random)
        {
            return GetRandomStepIndex(phase, previousStepIndex);
        }

        return (previousStepIndex + 1 + phase.behaviorLoop.Length) % phase.behaviorLoop.Length;
    }

    private int GetRandomStepIndex(BossPhase phase, int previousStepIndex)
    {
        int stepCount = phase.behaviorLoop.Length;

        if (stepCount <= 1)
            return 0;

        int nextStepIndex = UnityEngine.Random.Range(0, stepCount);

        if (!phase.avoidRandomRepeats || nextStepIndex != previousStepIndex)
            return nextStepIndex;

        return (nextStepIndex + UnityEngine.Random.Range(1, stepCount)) % stepCount;
    }

    private int GetPhaseIndexForHealth()
    {
        EnsurePhaseConfiguration();

        float healthPercent = CurrentHealthPercent;
        int bestPhaseIndex = 0;
        float bestThreshold = float.PositiveInfinity;

        for (int i = 0; i < phases.Length; i++)
        {
            BossPhase phase = phases[i];

            if (phase == null)
                continue;

            float threshold = Mathf.Clamp01(phase.enterAtHealthPercent);

            if (healthPercent <= threshold && threshold < bestThreshold)
            {
                bestThreshold = threshold;
                bestPhaseIndex = i;
            }
        }

        return bestPhaseIndex;
    }

    private bool IsPhaseRegression(int nextPhaseIndex)
    {
        if (!IsValidPhaseIndex(currentPhaseIndex) || !IsValidPhaseIndex(nextPhaseIndex))
            return false;

        float currentThreshold = Mathf.Clamp01(phases[currentPhaseIndex].enterAtHealthPercent);
        float nextThreshold = Mathf.Clamp01(phases[nextPhaseIndex].enterAtHealthPercent);
        return nextThreshold > currentThreshold;
    }

    private bool IsValidPhaseIndex(int phaseIndex)
    {
        return phases != null && phaseIndex >= 0 && phaseIndex < phases.Length && phases[phaseIndex] != null;
    }

    private void ResolvePlayerTransform(bool forceSearch = false)
    {
        if (playerTargetOverride != null)
        {
            playerTransform = playerTargetOverride;
            return;
        }

        if (playerTransform != null)
            return;

        if (!forceSearch && Time.time < nextPlayerSearchTime)
            return;

        nextPlayerSearchTime = Time.time + Mathf.Max(0.05f, playerSearchInterval);

        JonCharacterController jonCharacter = FindAnyObjectByType<JonCharacterController>();

        if (jonCharacter != null)
        {
            playerTransform = jonCharacter.transform;
            return;
        }

        Hero hero = FindAnyObjectByType<Hero>();

        if (hero != null)
        {
            playerTransform = hero.transform;
        }
    }

    private void StopHorizontalVelocityForRecovery()
    {
        if (!stopHorizontalVelocityDuringRecovery || rb2d == null)
            return;

        rb2d.linearVelocity = new Vector2(0f, rb2d.linearVelocity.y);
    }

    private GameObject GetHelperPrefab(int helperIndex)
    {
        if (helperPrefabs == null || helperPrefabs.Length == 0)
            return null;

        int prefabIndex = Mathf.Abs(helperIndex) % helperPrefabs.Length;
        return helperPrefabs[prefabIndex];
    }

    private Vector3 GetHelperSpawnPosition(int helperIndex)
    {
        if (helperSpawnPoints != null && helperSpawnPoints.Length > 0)
        {
            Transform spawnPoint = helperSpawnPoints[Mathf.Abs(helperIndex) % helperSpawnPoints.Length];

            if (spawnPoint != null)
            {
                return spawnPoint.position;
            }
        }

        float side = helperIndex % 2 == 0 ? 1f : -1f;
        float direction = (facingDirection == 0f ? 1f : facingDirection) * side;

        return transform.position + new Vector3(
            fallbackHelperSpawnOffset.x * direction,
            fallbackHelperSpawnOffset.y,
            0f
        );
    }

    private string GetCurrentPhaseName()
    {
        BossPhase phase = CurrentPhase;

        if (phase == null || string.IsNullOrEmpty(phase.phaseName))
            return "No Phase";

        return phase.phaseName;
    }

    private void DispatchStateEntered(BossBehaviorState state)
    {
        switch (state)
        {
            case BossBehaviorState.Intro:
                OnIntroStateEntered();
                break;
            case BossBehaviorState.Idle:
                OnIdleStateEntered();
                break;
            case BossBehaviorState.Attack:
                OnAttackStateEntered();
                break;
            case BossBehaviorState.JumpAttack:
                OnJumpAttackStateEntered();
                break;
            case BossBehaviorState.Recovery:
                OnRecoveryStateEntered();
                break;
            case BossBehaviorState.SummonHelpers:
                OnSummonHelpersStateEntered();
                break;
            case BossBehaviorState.PhaseTransition:
                OnPhaseTransitionStateEntered();
                break;
            case BossBehaviorState.Stunned:
                OnStunnedStateEntered();
                break;
        }
    }

    private void DispatchStateUpdated(BossBehaviorState state, float deltaTime)
    {
        switch (state)
        {
            case BossBehaviorState.Intro:
                OnIntroStateUpdated(deltaTime);
                break;
            case BossBehaviorState.Idle:
                OnIdleStateUpdated(deltaTime);
                break;
            case BossBehaviorState.Attack:
                OnAttackStateUpdated(deltaTime);
                break;
            case BossBehaviorState.JumpAttack:
                OnJumpAttackStateUpdated(deltaTime);
                break;
            case BossBehaviorState.Recovery:
                OnRecoveryStateUpdated(deltaTime);
                break;
            case BossBehaviorState.SummonHelpers:
                OnSummonHelpersStateUpdated(deltaTime);
                break;
            case BossBehaviorState.PhaseTransition:
                OnPhaseTransitionStateUpdated(deltaTime);
                break;
            case BossBehaviorState.Stunned:
                OnStunnedStateUpdated(deltaTime);
                break;
        }
    }

    private void DispatchStateFixedUpdated(BossBehaviorState state, float fixedDeltaTime)
    {
        switch (state)
        {
            case BossBehaviorState.Intro:
                OnIntroStateFixedUpdated(fixedDeltaTime);
                break;
            case BossBehaviorState.Idle:
                OnIdleStateFixedUpdated(fixedDeltaTime);
                break;
            case BossBehaviorState.Attack:
                OnAttackStateFixedUpdated(fixedDeltaTime);
                break;
            case BossBehaviorState.JumpAttack:
                OnJumpAttackStateFixedUpdated(fixedDeltaTime);
                break;
            case BossBehaviorState.Recovery:
                OnRecoveryStateFixedUpdated(fixedDeltaTime);
                break;
            case BossBehaviorState.SummonHelpers:
                OnSummonHelpersStateFixedUpdated(fixedDeltaTime);
                break;
            case BossBehaviorState.PhaseTransition:
                OnPhaseTransitionStateFixedUpdated(fixedDeltaTime);
                break;
            case BossBehaviorState.Stunned:
                OnStunnedStateFixedUpdated(fixedDeltaTime);
                break;
        }
    }

    private void DispatchStateExited(BossBehaviorState state)
    {
        switch (state)
        {
            case BossBehaviorState.Intro:
                OnIntroStateExited();
                break;
            case BossBehaviorState.Idle:
                OnIdleStateExited();
                break;
            case BossBehaviorState.Attack:
                OnAttackStateExited();
                break;
            case BossBehaviorState.JumpAttack:
                OnJumpAttackStateExited();
                break;
            case BossBehaviorState.Recovery:
                OnRecoveryStateExited();
                break;
            case BossBehaviorState.SummonHelpers:
                OnSummonHelpersStateExited();
                break;
            case BossBehaviorState.PhaseTransition:
                OnPhaseTransitionStateExited();
                break;
            case BossBehaviorState.Stunned:
                OnStunnedStateExited();
                break;
        }
    }

    private void OnValidate()
    {
        activationDelay = Mathf.Max(0f, activationDelay);
        playerSearchInterval = Mathf.Max(0.05f, playerSearchInterval);
        helpersPerSummon = Mathf.Max(0, helpersPerSummon);

        if (phases == null)
            return;

        foreach (BossPhase phase in phases)
        {
            if (phase == null)
                continue;

            phase.enterAtHealthPercent = Mathf.Clamp01(phase.enterAtHealthPercent);

            if (phase.behaviorLoop == null)
                continue;

            foreach (BossStateStep step in phase.behaviorLoop)
            {
                if (step != null)
                {
                    step.duration = Mathf.Max(0f, step.duration);
                }
            }
        }
    }
}
