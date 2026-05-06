using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class SceneMapConnection
{
    public string fromScene;
    public string toScene;

    public SceneMapConnection()
    {
    }

    public SceneMapConnection(string fromScene, string toScene)
    {
        this.fromScene = fromScene;
        this.toScene = toScene;
    }
}

public sealed class SceneMapGraph
{
    private readonly List<SceneMapConnection> connections;

    public string StartSceneName { get; }
    public IReadOnlyList<SceneMapConnection> Connections => connections;

    public SceneMapGraph(string startSceneName, IEnumerable<SceneMapConnection> sceneConnections)
    {
        connections = new List<SceneMapConnection>();
        HashSet<string> seenConnections = new HashSet<string>(StringComparer.Ordinal);

        if (sceneConnections != null)
        {
            foreach (SceneMapConnection connection in sceneConnections)
            {
                if (connection == null ||
                    string.IsNullOrWhiteSpace(connection.fromScene) ||
                    string.IsNullOrWhiteSpace(connection.toScene))
                {
                    continue;
                }

                string fromScene = connection.fromScene.Trim();
                string toScene = connection.toScene.Trim();
                string key = $"{fromScene}\u001f{toScene}";

                if (!seenConnections.Add(key))
                    continue;

                connections.Add(new SceneMapConnection(fromScene, toScene));
            }
        }

        StartSceneName = ResolveStartSceneName(startSceneName, connections);
    }

    public Dictionary<string, int> GetReachableDepths()
    {
        Dictionary<string, List<string>> outgoingScenes = BuildOutgoingScenes();
        Dictionary<string, int> depths = new Dictionary<string, int>(StringComparer.Ordinal);
        Queue<string> pendingScenes = new Queue<string>();

        if (string.IsNullOrWhiteSpace(StartSceneName))
            return depths;

        depths[StartSceneName] = 0;
        pendingScenes.Enqueue(StartSceneName);

        while (pendingScenes.Count > 0)
        {
            string currentScene = pendingScenes.Dequeue();
            if (!outgoingScenes.TryGetValue(currentScene, out List<string> nextScenes))
                continue;

            int nextDepth = depths[currentScene] + 1;
            foreach (string nextScene in nextScenes)
            {
                if (depths.ContainsKey(nextScene))
                    continue;

                depths[nextScene] = nextDepth;
                pendingScenes.Enqueue(nextScene);
            }
        }

        return depths;
    }

    public List<string> GetReachableSceneNames()
    {
        Dictionary<string, int> depths = GetReachableDepths();
        List<string> sceneNames = new List<string>(depths.Keys);

        sceneNames.Sort((left, right) =>
        {
            int depthCompare = depths[left].CompareTo(depths[right]);
            return depthCompare != 0
                ? depthCompare
                : string.Compare(left, right, StringComparison.Ordinal);
        });

        return sceneNames;
    }

    public List<SceneMapConnection> GetReachableConnections()
    {
        Dictionary<string, int> depths = GetReachableDepths();
        List<SceneMapConnection> reachableConnections = new List<SceneMapConnection>();

        foreach (SceneMapConnection connection in connections)
        {
            if (!depths.ContainsKey(connection.fromScene) || !depths.ContainsKey(connection.toScene))
                continue;

            reachableConnections.Add(connection);
        }

        return reachableConnections;
    }

    private Dictionary<string, List<string>> BuildOutgoingScenes()
    {
        Dictionary<string, List<string>> outgoingScenes =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (SceneMapConnection connection in connections)
        {
            if (!outgoingScenes.TryGetValue(connection.fromScene, out List<string> nextScenes))
            {
                nextScenes = new List<string>();
                outgoingScenes.Add(connection.fromScene, nextScenes);
            }

            if (!nextScenes.Contains(connection.toScene))
            {
                nextScenes.Add(connection.toScene);
            }
        }

        return outgoingScenes;
    }

    private static string ResolveStartSceneName(string configuredStartScene, List<SceneMapConnection> sceneConnections)
    {
        string trimmedStartScene = string.IsNullOrWhiteSpace(configuredStartScene)
            ? SceneMapDataLoader.DefaultStartSceneName
            : configuredStartScene.Trim();

        if (ContainsScene(trimmedStartScene, sceneConnections))
            return trimmedStartScene;

        if (string.Equals(trimmedStartScene, SceneMapDataLoader.CastleOneDisplayName, StringComparison.Ordinal) &&
            ContainsScene(SceneMapDataLoader.DefaultStartSceneName, sceneConnections))
        {
            return SceneMapDataLoader.DefaultStartSceneName;
        }

        if (ContainsScene(SceneMapDataLoader.DefaultStartSceneName, sceneConnections))
            return SceneMapDataLoader.DefaultStartSceneName;

        return trimmedStartScene;
    }

    private static bool ContainsScene(string sceneName, List<SceneMapConnection> sceneConnections)
    {
        foreach (SceneMapConnection connection in sceneConnections)
        {
            if (string.Equals(connection.fromScene, sceneName, StringComparison.Ordinal) ||
                string.Equals(connection.toScene, sceneName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public static class SceneMapDataLoader
{
    public const string CastleOneDisplayName = "Castle1";
    public const string DefaultStartSceneName = "CastleStart";

    private const string SceneMapResourceName = "SceneNetworkMap";

    private static SceneMapGraph cachedGraph;

    public static SceneMapGraph Load()
    {
        if (cachedGraph != null)
            return cachedGraph;

        TextAsset mapDataAsset = Resources.Load<TextAsset>(SceneMapResourceName);
        if (mapDataAsset != null)
        {
            try
            {
                SceneMapJson mapJson = JsonUtility.FromJson<SceneMapJson>(mapDataAsset.text);
                if (mapJson != null)
                {
                    cachedGraph = CreateGraph(mapJson.startSceneName, mapJson.connections);
                }
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"Could not parse {SceneMapResourceName}: {exception.Message}");
            }
        }

        cachedGraph ??= BuildFallbackGraphFromLoadedScene();
        return cachedGraph;
    }

    public static SceneMapGraph CreateGraph(string startSceneName, IEnumerable<SceneMapConnection> connections)
    {
        return new SceneMapGraph(startSceneName, connections);
    }

    public static string ToJson(string startSceneName, IEnumerable<SceneMapConnection> connections)
    {
        SceneMapJson mapJson = new SceneMapJson
        {
            startSceneName = string.IsNullOrWhiteSpace(startSceneName)
                ? DefaultStartSceneName
                : startSceneName.Trim(),
            connections = ToArray(connections)
        };

        return JsonUtility.ToJson(mapJson, true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearCacheOnPlayModeStart()
    {
        cachedGraph = null;
    }

    private static SceneMapGraph BuildFallbackGraphFromLoadedScene()
    {
        List<SceneMapConnection> connections = new List<SceneMapConnection>();
        RoomTransition[] transitions =
            UnityEngine.Object.FindObjectsByType<RoomTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (RoomTransition transition in transitions)
        {
            if (transition == null || string.IsNullOrWhiteSpace(transition.sceneToLoad))
                continue;

            Scene transitionScene = transition.gameObject.scene;
            string fromScene = transitionScene.IsValid()
                ? transitionScene.name
                : SceneManager.GetActiveScene().name;

            connections.Add(new SceneMapConnection(fromScene, transition.sceneToLoad));
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        string startSceneName = !string.IsNullOrWhiteSpace(activeSceneName)
            ? activeSceneName
            : DefaultStartSceneName;

        return CreateGraph(startSceneName, connections);
    }

    private static SceneMapConnection[] ToArray(IEnumerable<SceneMapConnection> connections)
    {
        if (connections == null)
            return Array.Empty<SceneMapConnection>();

        List<SceneMapConnection> connectionList = new List<SceneMapConnection>();
        foreach (SceneMapConnection connection in connections)
        {
            if (connection != null)
            {
                connectionList.Add(connection);
            }
        }

        return connectionList.ToArray();
    }

    [Serializable]
    private sealed class SceneMapJson
    {
        public string startSceneName = DefaultStartSceneName;
        public SceneMapConnection[] connections = Array.Empty<SceneMapConnection>();
    }
}
