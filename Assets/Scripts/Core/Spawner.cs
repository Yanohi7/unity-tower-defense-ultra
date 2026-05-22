using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    // Spawner controls enemy waves.
    // In Adventure mode, each route has its own wave list.
    // Waves with the same index on different routes start at the same time.
    // Empty wave slots can be used when a route should not spawn enemies during a specific wave.

    public enum SpawnMode
    {
        Adventure,
        EndlessAI,
        HotSeat
    }

    public enum BudgetGrowthMode
    {
        Linear,
        Progressive
    }

    // Class representing an enemy option for AI and HotSeat mode.
    // Enemy prefab and cost are taken from EnemyData to avoid duplicated values.
    [System.Serializable]
    public class EnemyOption
    {
        public EnemyData enemyData;
        public int unlockFromWave = 1;

        [Range(0.1f, 5f)]
        public float weight = 1f;
    }

    // Route contains one spawn point, one path, and its own wave list.
    // Each route can spawn different enemies on the same global wave number.
    [System.Serializable]
    public class Route
    {
        public string routeName;
        public Transform spawnPoint;
        public Transform[] waypoints;

        [Header("Waves for this route")]
        public WaveSO[] waves;
    }

    [Header("Mode")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Adventure;

    [Header("Routes")]
    [SerializeField] private Route[] routes;

    [Header("AI / HotSeat - Enemies")]
    [SerializeField] private EnemyOption[] aiEnemyOptions;

    [Header("AI / HotSeat - Budget")]
    [SerializeField] private BudgetGrowthMode budgetGrowthMode = BudgetGrowthMode.Progressive;
    [SerializeField] private int startAttackBudget = 6;
    [SerializeField] private int budgetIncreasePerWave = 3;
    [SerializeField] private float earlyBudgetMultiplier = 1.35f;
    [SerializeField] private float lateBudgetMultiplier = 1.18f;
    [SerializeField] private int lateGameStartsFromWave = 6;
    [SerializeField] private int maxAttackBudget = 150;

    [Header("AI / HotSeat - Spawn Delay")]
    [SerializeField] private float minSpawnDelay = 0.5f;
    [SerializeField] private float maxSpawnDelay = 3f;

    [Header("Endless AI - Scaling")]
    [SerializeField] private int hpScalingStartsFromWave = 10;
    [SerializeField] private int hpScalingEveryWaves = 5;
    [SerializeField] private float hpIncreasePerStep = 0.1f;

    [SerializeField] private float baseGoldMultiplier = 1f;
    [SerializeField] private float goldGrowthFromHpMultiplier = 0.65f;

    [Header("Endless AI - Special Waves")]
    [SerializeField] private int specialWaveEvery = 5;
    [SerializeField] private float specialWaveBudgetMultiplier = 1.35f;
    [SerializeField] private float specialWaveHpMultiplier = 1.15f;

    public bool isSpawning { get; private set; }
    public int CurrentWaveNumber => currentWaveIndex;

    private int currentWaveIndex = 0;
    private Coroutine spawnRoutine;

    // Route coroutines are stored so spawning can be stopped correctly when the game ends.
    private readonly List<Coroutine> routeSpawnRoutines = new List<Coroutine>();

    // HotSeat queue stores EnemyData selected by the attacking player.
    private readonly List<EnemyData> hotSeatEnemyQueue = new List<EnemyData>();

    private void Start()
    {
        isSpawning = false;
    }

    // Method to check if the spawner is currently using adventure mode.
    public bool IsAdventureMode()
    {
        return spawnMode == SpawnMode.Adventure;
    }

    // Method to check if the spawner is currently using endless AI mode.
    public bool IsEndlessMode()
    {
        return spawnMode == SpawnMode.EndlessAI;
    }

    // Method to check if the spawner is currently using HotSeat mode.
    public bool IsHotSeatMode()
    {
        return spawnMode == SpawnMode.HotSeat;
    }

    // Method to check if there are still adventure waves left.
    public bool HasMoreAdventureWaves()
    {
        return currentWaveIndex < GetMaxAdventureWaveCount();
    }

    // Method to check if the next wave can be started.
    public bool CanStartNextWave()
    {
        if (isSpawning)
            return false;

        if (spawnMode == SpawnMode.Adventure)
            return HasMoreAdventureWaves();

        if (spawnMode == SpawnMode.HotSeat)
            return hotSeatEnemyQueue.Count > 0;

        return true;
    }

    // Method to start the next wave depending on the selected mode.
    public void StartNextWave()
    {
        if (!CanStartNextWave())
            return;

        switch (spawnMode)
        {
            case SpawnMode.Adventure:
                StartNextAdventureWave();
                break;

            case SpawnMode.EndlessAI:
                StartNextAIWave();
                break;

            case SpawnMode.HotSeat:
                StartNextHotSeatWave();
                break;
        }
    }

    // Method to stop current spawning coroutines if the game ends.
    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        foreach (Coroutine routine in routeSpawnRoutines)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        routeSpawnRoutines.Clear();
        isSpawning = false;
    }

    // Method to get the number of enemy options available for UI buttons.
    public int GetEnemyOptionCount()
    {
        if (aiEnemyOptions == null)
            return 0;

        return aiEnemyOptions.Length;
    }

    // Method to get enemy option by index.
    public EnemyOption GetEnemyOption(int index)
    {
        if (aiEnemyOptions == null)
            return null;

        if (index < 0 || index >= aiEnemyOptions.Length)
            return null;

        return aiEnemyOptions[index];
    }

    // Method to check if an enemy option is unlocked for the next wave.
    public bool IsEnemyOptionUnlockedForNextWave(int index)
    {
        EnemyOption option = GetEnemyOption(index);

        if (option == null || option.enemyData == null || option.enemyData.prefab == null)
            return false;

        return GetNextWaveNumber() >= option.unlockFromWave;
    }

    // Method to get the wave number that is currently being prepared.
    public int GetNextWaveNumber()
    {
        return currentWaveIndex + 1;
    }

    // Method to get the attack budget for the next wave.
    public int GetNextWaveAttackBudget()
    {
        if (spawnMode == SpawnMode.HotSeat)
            return CalculateBaseAttackBudget(GetNextWaveNumber());

        return CalculateFinalAIAttackBudget(GetNextWaveNumber());
    }

    // Method to calculate how many attack points are already spent by HotSeat player.
    public int GetHotSeatSpentBudget()
    {
        int spent = 0;

        foreach (EnemyData enemyData in hotSeatEnemyQueue)
        {
            if (enemyData != null)
                spent += enemyData.attackPointCost;
        }

        return spent;
    }

    // Method to get remaining HotSeat attack budget.
    public int GetHotSeatRemainingBudget()
    {
        return GetNextWaveAttackBudget() - GetHotSeatSpentBudget();
    }

    // Method to get how many enemies are currently selected by HotSeat attacker.
    public int GetHotSeatSelectedEnemyCount()
    {
        return hotSeatEnemyQueue.Count;
    }

    // Method to add an enemy to the HotSeat wave by option index.
    public bool TryAddHotSeatEnemyByIndex(int optionIndex)
    {
        if (spawnMode != SpawnMode.HotSeat)
            return false;

        EnemyOption option = GetEnemyOption(optionIndex);

        if (option == null || option.enemyData == null || option.enemyData.prefab == null)
            return false;

        if (!IsEnemyOptionUnlockedForNextWave(optionIndex))
            return false;

        if (option.enemyData.attackPointCost > GetHotSeatRemainingBudget())
            return false;

        hotSeatEnemyQueue.Add(option.enemyData);
        return true;
    }

    // Method to remove the last selected enemy from the HotSeat wave.
    public void RemoveLastHotSeatEnemy()
    {
        if (hotSeatEnemyQueue.Count <= 0)
            return;

        hotSeatEnemyQueue.RemoveAt(hotSeatEnemyQueue.Count - 1);
    }

    // Method to clear the whole HotSeat wave.
    public void ClearHotSeatWave()
    {
        hotSeatEnemyQueue.Clear();
    }

    // Method to get the largest wave count from all routes.
    // This makes the global wave count depend on the longest route wave list.
    private int GetMaxAdventureWaveCount()
    {
        int maxWaveCount = 0;

        if (routes == null)
            return maxWaveCount;

        foreach (Route route in routes)
        {
            if (route == null || route.waves == null)
                continue;

            if (route.waves.Length > maxWaveCount)
                maxWaveCount = route.waves.Length;
        }

        return maxWaveCount;
    }

    // Method to start the next fixed adventure wave on all routes.
    private void StartNextAdventureWave()
    {
        int waveIndex = currentWaveIndex;
        currentWaveIndex++;

        spawnRoutine = StartCoroutine(SpawnAdventureWaveOnAllRoutes(waveIndex));
    }

    // Coroutine that starts the same wave index on every route at the same time.
    private IEnumerator SpawnAdventureWaveOnAllRoutes(int waveIndex)
    {
        isSpawning = true;
        routeSpawnRoutines.Clear();

        int activeRouteSpawns = 0;

        if (routes != null)
        {
            foreach (Route route in routes)
            {
                if (!IsValidRoute(route))
                    continue;

                if (route.waves == null)
                    continue;

                if (waveIndex < 0 || waveIndex >= route.waves.Length)
                    continue;

                WaveSO wave = route.waves[waveIndex];

                if (wave == null || wave.enemyQueue == null)
                    continue;

                activeRouteSpawns++;

                Coroutine routine = StartCoroutine(SpawnRouteWave(route, wave, () =>
                {
                    activeRouteSpawns--;
                }));

                routeSpawnRoutines.Add(routine);
            }
        }

        while (activeRouteSpawns > 0)
            yield return null;

        routeSpawnRoutines.Clear();
        isSpawning = false;
        spawnRoutine = null;
    }

    // Coroutine that spawns one wave on one specific route.
    private IEnumerator SpawnRouteWave(Route route, WaveSO wave, System.Action onFinished)
    {
        foreach (WaveEnemyEntry entry in wave.enemyQueue)
        {
            if (entry == null || entry.enemyPrefab == null)
                continue;

            SpawnEnemy(entry.enemyPrefab, wave.hpMultiplier, wave.goldMultiplier, route);

            yield return new WaitForSeconds(entry.spawnDelayAfter);
        }

        onFinished?.Invoke();
    }

    // Method to generate and start the next endless AI wave.
    private void StartNextAIWave()
    {
        currentWaveIndex++;

        int budget = CalculateFinalAIAttackBudget(currentWaveIndex);
        float hpMultiplier = CalculateFinalAIHpMultiplier(currentWaveIndex);
        float goldMultiplier = CalculateGoldMultiplier(hpMultiplier);

        List<GameObject> generatedEnemies = GenerateAIEnemies(budget);

        spawnRoutine = StartCoroutine(SpawnGeneratedWave(generatedEnemies, hpMultiplier, goldMultiplier));
    }

    // Method to start the HotSeat wave prepared by the attacking player.
    private void StartNextHotSeatWave()
    {
        if (hotSeatEnemyQueue.Count <= 0)
            return;

        currentWaveIndex++;

        float hpMultiplier = 1f;
        float goldMultiplier = 1f;

        List<EnemyData> waveEnemies = new List<EnemyData>(hotSeatEnemyQueue);
        hotSeatEnemyQueue.Clear();

        spawnRoutine = StartCoroutine(SpawnHotSeatWave(waveEnemies, hpMultiplier, goldMultiplier));
    }

    // Coroutine that spawns a HotSeat wave from selected EnemyData.
    // HotSeat mode uses random valid routes.
    private IEnumerator SpawnHotSeatWave(List<EnemyData> enemies, float hpMultiplier, float goldMultiplier)
    {
        isSpawning = true;

        foreach (EnemyData enemyData in enemies)
        {
            if (enemyData == null || enemyData.prefab == null)
                continue;

            Route route = GetRandomValidRoute();
            SpawnEnemy(enemyData.prefab, hpMultiplier, goldMultiplier, route);

            float randomDelay = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(randomDelay);
        }

        isSpawning = false;
        spawnRoutine = null;
    }

    // Coroutine that spawns a generated list of enemies with random delays.
    // Endless AI mode uses random valid routes.
    private IEnumerator SpawnGeneratedWave(List<GameObject> enemies, float hpMultiplier, float goldMultiplier)
    {
        isSpawning = true;

        foreach (GameObject enemyPrefab in enemies)
        {
            Route route = GetRandomValidRoute();
            SpawnEnemy(enemyPrefab, hpMultiplier, goldMultiplier, route);

            float randomDelay = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(randomDelay);
        }

        isSpawning = false;
        spawnRoutine = null;
    }

    // Method to check if a route has all required path data.
    private bool IsValidRoute(Route route)
    {
        if (route == null)
            return false;

        if (route.spawnPoint == null)
            return false;

        if (route.waypoints == null || route.waypoints.Length == 0)
            return false;

        return true;
    }

    // Method to get a random route that has valid path data.
    private Route GetRandomValidRoute()
    {
        if (routes == null || routes.Length == 0)
            return null;

        List<Route> validRoutes = new List<Route>();

        foreach (Route route in routes)
        {
            if (IsValidRoute(route))
                validRoutes.Add(route);
        }

        if (validRoutes.Count == 0)
            return null;

        return validRoutes[Random.Range(0, validRoutes.Count)];
    }

    // Method to spawn or reuse an enemy from the object pool.
    private void SpawnEnemy(GameObject enemyPrefab, float hpMultiplier, float goldMultiplier, Route route)
    {
        if (enemyPrefab == null)
            return;

        if (!IsValidRoute(route))
        {
            Debug.LogWarning($"{gameObject.name}: route is missing spawn point or waypoints.");
            return;
        }

        GameObject enemyGO;

        if (ObjectPooler.Instance != null)
        {
            enemyGO = ObjectPooler.Instance.Spawn(
                enemyPrefab,
                route.spawnPoint.position,
                Quaternion.identity
            );
        }
        else
        {
            enemyGO = Instantiate(
                enemyPrefab,
                route.spawnPoint.position,
                Quaternion.identity
            );
        }

        Enemy enemy = enemyGO.GetComponent<Enemy>();

        if (enemy == null)
        {
            Debug.LogWarning($"{enemyPrefab.name} has no Enemy component!");

            if (ObjectPooler.Instance != null)
                ObjectPooler.Instance.ReturnToPool(enemyGO);
            else
                Destroy(enemyGO);

            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.RegisterEnemy();

        enemy.OnDeath -= EnemyFinished;
        enemy.OnDeath += EnemyFinished;

        enemy.Initialize(enemy.EnemyData, route.waypoints);
        enemy.SetHPMultiplier(hpMultiplier);
        enemy.SetGoldMultiplier(goldMultiplier);
    }

    // Method called when an enemy dies or reaches the end of the path.
    private void EnemyFinished()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterEnemy();
    }

    // Method to calculate final AI attack budget including special wave multiplier.
    private int CalculateFinalAIAttackBudget(int wave)
    {
        int budget = CalculateBaseAttackBudget(wave);

        bool specialWave = specialWaveEvery > 0 && wave % specialWaveEvery == 0;

        if (specialWave)
            budget = Mathf.RoundToInt(budget * specialWaveBudgetMultiplier);

        return Mathf.Min(budget, maxAttackBudget);
    }

    // Method to calculate base attack budget without special wave bonus.
    private int CalculateBaseAttackBudget(int wave)
    {
        if (budgetGrowthMode == BudgetGrowthMode.Linear)
        {
            int budget = startAttackBudget + (wave - 1) * budgetIncreasePerWave;
            return Mathf.Min(budget, maxAttackBudget);
        }

        float budgetValue = startAttackBudget;

        for (int i = 2; i <= wave; i++)
        {
            float multiplier = i < lateGameStartsFromWave
                ? earlyBudgetMultiplier
                : lateBudgetMultiplier;

            budgetValue *= multiplier;
        }

        return Mathf.Min(Mathf.RoundToInt(budgetValue), maxAttackBudget);
    }

    // Method to calculate final AI HP scaling including special wave multiplier.
    private float CalculateFinalAIHpMultiplier(int wave)
    {
        float hpMultiplier = CalculateBaseAIHpMultiplier(wave);

        bool specialWave = specialWaveEvery > 0 && wave % specialWaveEvery == 0;

        if (specialWave)
            hpMultiplier *= specialWaveHpMultiplier;

        return hpMultiplier;
    }

    // Method to calculate HP scaling for later AI waves.
    private float CalculateBaseAIHpMultiplier(int wave)
    {
        if (wave < hpScalingStartsFromWave)
            return 1f;

        int passedWaves = wave - hpScalingStartsFromWave;
        int steps = passedWaves / hpScalingEveryWaves + 1;

        return 1f + steps * hpIncreasePerStep;
    }

    // Method to calculate gold reward scaling based on enemy HP scaling.
    private float CalculateGoldMultiplier(float hpMultiplier)
    {
        return baseGoldMultiplier + (hpMultiplier - 1f) * goldGrowthFromHpMultiplier;
    }

    // Method to generate a list of enemy prefabs using the current attack budget.
    private List<GameObject> GenerateAIEnemies(int budget)
    {
        List<GameObject> result = new List<GameObject>();
        List<EnemyOption> availableEnemies = new List<EnemyOption>();

        if (aiEnemyOptions == null)
            return result;

        foreach (EnemyOption option in aiEnemyOptions)
        {
            if (option == null || option.enemyData == null || option.enemyData.prefab == null)
                continue;

            if (currentWaveIndex >= option.unlockFromWave && option.enemyData.attackPointCost <= budget)
                availableEnemies.Add(option);
        }

        if (availableEnemies.Count == 0)
            return result;

        int safety = 1000;

        while (budget > 0 && safety > 0)
        {
            safety--;

            List<EnemyOption> affordable = new List<EnemyOption>();

            foreach (EnemyOption option in availableEnemies)
            {
                if (option.enemyData != null && option.enemyData.attackPointCost <= budget)
                    affordable.Add(option);
            }

            if (affordable.Count == 0)
                break;

            EnemyOption chosen = PickWeightedEnemy(affordable);

            result.Add(chosen.enemyData.prefab);
            budget -= chosen.enemyData.attackPointCost;
        }

        return result;
    }

    // Method to pick a random enemy option based on weights.
    private EnemyOption PickWeightedEnemy(List<EnemyOption> options)
    {
        float totalWeight = 0f;

        foreach (EnemyOption option in options)
            totalWeight += option.weight;

        float randomValue = Random.Range(0f, totalWeight);

        foreach (EnemyOption option in options)
        {
            randomValue -= option.weight;

            if (randomValue <= 0f)
                return option;
        }

        return options[options.Count - 1];
    }
}