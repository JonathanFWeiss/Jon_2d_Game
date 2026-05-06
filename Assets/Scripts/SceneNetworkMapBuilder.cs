#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNetworkMapBuilder
{
    private const string MenuPath = "Tools/Scene Map/Rebuild Castle Scene Map";
    private const string SceneSearchRoot = "Assets/Scenes";
    private const string ResourceAssetPath = "Assets/Resources/SceneNetworkMap.json";

    [MenuItem(MenuPath)]
    public static void RebuildCastleSceneMap()
    {
        Dictionary<string, string> scenePathsByName = BuildScenePathLookup();
        string startSceneName = ResolveStartSceneName(scenePathsByName);

        if (string.IsNullOrWhiteSpace(startSceneName) ||
            !scenePathsByName.ContainsKey(startSceneName))
        {
            Debug.LogError("Scene map could not find Castle1 or CastleStart under Assets/Scenes.");
            return;
        }

        SceneMapGraph graph = BuildGraph(startSceneName, scenePathsByName);
        string json = SceneMapDataLoader.ToJson(graph.StartSceneName, graph.Connections);

        File.WriteAllText(ResourceAssetPath, json + System.Environment.NewLine);
        AssetDatabase.ImportAsset(ResourceAssetPath);
        Debug.Log($"Rebuilt scene map with {graph.Connections.Count} connection(s) starting at {graph.StartSceneName}.");
    }

    private static Dictionary<string, string> BuildScenePathLookup()
    {
        Dictionary<string, string> scenePathsByName = new Dictionary<string, string>(System.StringComparer.Ordinal);
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { SceneSearchRoot });

        foreach (string sceneGuid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);

            if (string.IsNullOrWhiteSpace(sceneName) || scenePathsByName.ContainsKey(sceneName))
                continue;

            scenePathsByName.Add(sceneName, scenePath);
        }

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                continue;

            string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
            if (!scenePathsByName.ContainsKey(sceneName))
            {
                scenePathsByName.Add(sceneName, buildScene.path);
            }
        }

        return scenePathsByName;
    }

    private static string ResolveStartSceneName(Dictionary<string, string> scenePathsByName)
    {
        if (scenePathsByName.ContainsKey(SceneMapDataLoader.CastleOneDisplayName))
            return SceneMapDataLoader.CastleOneDisplayName;

        if (scenePathsByName.ContainsKey(SceneMapDataLoader.DefaultStartSceneName))
            return SceneMapDataLoader.DefaultStartSceneName;

        return null;
    }

    private static SceneMapGraph BuildGraph(string startSceneName, Dictionary<string, string> scenePathsByName)
    {
        List<SceneMapConnection> connections = new List<SceneMapConnection>();
        Queue<string> pendingScenes = new Queue<string>();
        HashSet<string> visitedScenes = new HashSet<string>(System.StringComparer.Ordinal);

        pendingScenes.Enqueue(startSceneName);

        while (pendingScenes.Count > 0)
        {
            string currentSceneName = pendingScenes.Dequeue();
            if (!visitedScenes.Add(currentSceneName))
                continue;

            if (!scenePathsByName.TryGetValue(currentSceneName, out string scenePath))
                continue;

            foreach (string nextSceneName in ReadOutgoingScenes(scenePath))
            {
                connections.Add(new SceneMapConnection(currentSceneName, nextSceneName));

                if (scenePathsByName.ContainsKey(nextSceneName) && !visitedScenes.Contains(nextSceneName))
                {
                    pendingScenes.Enqueue(nextSceneName);
                }
            }
        }

        return SceneMapDataLoader.CreateGraph(startSceneName, connections);
    }

    private static IEnumerable<string> ReadOutgoingScenes(string scenePath)
    {
        Scene scene = OpenSceneForRead(scenePath, out bool openedByBuilder);

        try
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                RoomTransition[] transitions = rootObject.GetComponentsInChildren<RoomTransition>(true);
                foreach (RoomTransition transition in transitions)
                {
                    if (transition != null && !string.IsNullOrWhiteSpace(transition.sceneToLoad))
                    {
                        yield return transition.sceneToLoad.Trim();
                    }
                }
            }
        }
        finally
        {
            if (openedByBuilder)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static Scene OpenSceneForRead(string scenePath, out bool openedByBuilder)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.path == scenePath)
            {
                openedByBuilder = false;
                return loadedScene;
            }
        }

        openedByBuilder = true;
        return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
    }
}
#endif
