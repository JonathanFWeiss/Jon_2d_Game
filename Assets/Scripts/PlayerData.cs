using System.Collections;
using UnityEngine;

public static class PlayerData
{
    public const int DefaultHP = 3;
    private const float RemoveHpCooldownSeconds = 1f;
    private const float HpRemovalPhysicsPauseSeconds = 0.5f;

    public static int Coins { get; private set; }
    public static int HP { get; private set; } = DefaultHP;

    private static float nextAllowedHpRemovalTime = 0f;
    private static PhysicsPauseRunner physicsPauseRunner;

    public static void AddCoins(int amount = 1)
    {
        Coins += amount;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeOnPlayModeStart()
    {
        Coins = 0;
        HP = DefaultHP;
        nextAllowedHpRemovalTime = 0f;
        physicsPauseRunner = null;
    }

    public static void RemoveHP(int amount = 1)
    {// Prevent HP removal if the amount is not positive or if we're still in the cooldown period

        Debug.Log("Current time: " + Time.time + ", Next allowed HP removal time: " + nextAllowedHpRemovalTime);
        if (amount <= 0 || Time.time < nextAllowedHpRemovalTime)
            return;

        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
        HP -= amount;
        HP = Mathf.Max(HP, 0);
        PausePhysicsAfterHpRemoval();
        Debug.Log("Player HP: " + HP);

    }

    public static void RestoreFullHP()
    {
        HP = DefaultHP;
        Debug.Log("Player HP restored to: " + HP);
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
    }

    public static void Reset()
    {
        Coins = 0;
        HP = DefaultHP;
        nextAllowedHpRemovalTime = Time.time + RemoveHpCooldownSeconds;
    }

    private static void PausePhysicsAfterHpRemoval()
    {
        if (HpRemovalPhysicsPauseSeconds <= 0f)
            return;

        GetOrCreatePhysicsPauseRunner().PauseForSeconds(HpRemovalPhysicsPauseSeconds);
    }

    private static PhysicsPauseRunner GetOrCreatePhysicsPauseRunner()
    {
        if (physicsPauseRunner != null)
            return physicsPauseRunner;

        GameObject runnerObject = new GameObject("PlayerDataPhysicsPauseRunner");
        runnerObject.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(runnerObject);
        physicsPauseRunner = runnerObject.AddComponent<PhysicsPauseRunner>();
        return physicsPauseRunner;
    }

    private sealed class PhysicsPauseRunner : MonoBehaviour
    {
        private Coroutine pauseCoroutine;
        private bool isPhysicsPaused;
        private SimulationMode2D pausedPhysics2DMode;
        private SimulationMode pausedPhysicsMode;

        public void PauseForSeconds(float duration)
        {
            if (!isPhysicsPaused)
            {
                pausedPhysics2DMode = Physics2D.simulationMode;
                pausedPhysicsMode = Physics.simulationMode;
                Physics2D.simulationMode = SimulationMode2D.Script;
                Physics.simulationMode = SimulationMode.Script;
                isPhysicsPaused = true;
            }

            if (pauseCoroutine != null)
            {
                StopCoroutine(pauseCoroutine);
            }

            pauseCoroutine = StartCoroutine(PauseRoutine(duration));
        }

        private IEnumerator PauseRoutine(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);

            Physics2D.simulationMode = pausedPhysics2DMode;
            Physics.simulationMode = pausedPhysicsMode;
            isPhysicsPaused = false;
            pauseCoroutine = null;
        }

        private void OnDestroy()
        {
            if (isPhysicsPaused)
            {
                Physics2D.simulationMode = pausedPhysics2DMode;
                Physics.simulationMode = pausedPhysicsMode;
            }

            if (physicsPauseRunner == this)
            {
                physicsPauseRunner = null;
            }
        }
    }
}
