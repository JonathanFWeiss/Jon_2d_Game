using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class SceneMapOverlay
{
    private const string OverlayRootName = "SceneMapOverlay";
    private const string TitleText = "Castle Map";
    private const string CurrentPrefix = "Current: ";

    public static bool Show(UIDocument uiDocument, SceneMapGraph mapGraph, string currentSceneName)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return false;

        Hide(uiDocument);

        VisualElement overlay = new VisualElement
        {
            name = OverlayRootName,
            pickingMode = PickingMode.Ignore
        };

        overlay.style.position = Position.Absolute;
        overlay.style.top = 0f;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.bottom = 0f;
        overlay.style.paddingLeft = 28f;
        overlay.style.paddingRight = 28f;
        overlay.style.paddingTop = 28f;
        overlay.style.paddingBottom = 28f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.76f);

        VisualElement panel = CreatePanel();
        panel.Add(CreateHeader(currentSceneName));

        SceneMapGraphView graphView = new SceneMapGraphView();
        graphView.Populate(mapGraph, currentSceneName);
        panel.Add(graphView);

        overlay.Add(panel);
        uiDocument.rootVisualElement.Add(overlay);
        return true;
    }

    public static void Hide(UIDocument uiDocument)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
            return;

        VisualElement existingOverlay = uiDocument.rootVisualElement.Q<VisualElement>(OverlayRootName);
        if (existingOverlay != null)
        {
            existingOverlay.RemoveFromHierarchy();
        }
    }

    public static string GetDisplaySceneName(string sceneName)
    {
        if (string.Equals(sceneName, SceneMapDataLoader.DefaultStartSceneName, StringComparison.Ordinal))
            return SceneMapDataLoader.CastleOneDisplayName;

        return string.IsNullOrWhiteSpace(sceneName) ? "Unknown" : sceneName.Trim();
    }

    private static VisualElement CreatePanel()
    {
        VisualElement panel = new VisualElement
        {
            pickingMode = PickingMode.Ignore
        };

        panel.style.width = Length.Percent(86f);
        panel.style.height = Length.Percent(78f);
        panel.style.maxWidth = 920f;
        panel.style.maxHeight = 620f;
        panel.style.minHeight = 360f;
        panel.style.paddingLeft = 26f;
        panel.style.paddingRight = 26f;
        panel.style.paddingTop = 22f;
        panel.style.paddingBottom = 26f;
        panel.style.backgroundColor = new Color(0.045f, 0.048f, 0.046f, 0.96f);
        panel.style.borderTopWidth = 1f;
        panel.style.borderRightWidth = 1f;
        panel.style.borderBottomWidth = 1f;
        panel.style.borderLeftWidth = 1f;
        panel.style.borderTopColor = new Color(0.65f, 0.62f, 0.52f, 0.92f);
        panel.style.borderRightColor = new Color(0.65f, 0.62f, 0.52f, 0.92f);
        panel.style.borderBottomColor = new Color(0.65f, 0.62f, 0.52f, 0.92f);
        panel.style.borderLeftColor = new Color(0.65f, 0.62f, 0.52f, 0.92f);
        panel.style.borderTopLeftRadius = 8f;
        panel.style.borderTopRightRadius = 8f;
        panel.style.borderBottomLeftRadius = 8f;
        panel.style.borderBottomRightRadius = 8f;

        return panel;
    }

    private static VisualElement CreateHeader(string currentSceneName)
    {
        VisualElement header = new VisualElement
        {
            pickingMode = PickingMode.Ignore
        };

        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 18f;

        Label title = new Label(TitleText)
        {
            pickingMode = PickingMode.Ignore
        };

        title.style.color = new Color(0.96f, 0.91f, 0.78f, 1f);
        title.style.fontSize = 26f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;

        Label currentScene = new Label(CurrentPrefix + GetDisplaySceneName(currentSceneName))
        {
            pickingMode = PickingMode.Ignore
        };

        currentScene.style.color = new Color(0.74f, 0.88f, 0.84f, 1f);
        currentScene.style.fontSize = 16f;
        currentScene.style.unityTextAlign = TextAnchor.MiddleRight;

        header.Add(title);
        header.Add(currentScene);
        return header;
    }

    private sealed class SceneMapGraphView : VisualElement
    {
        private static readonly Color EdgeColor = new Color(0.58f, 0.72f, 0.68f, 0.82f);
        private static readonly Color NormalFillColor = new Color(0.12f, 0.13f, 0.12f, 0.96f);
        private static readonly Color NormalBorderColor = new Color(0.67f, 0.71f, 0.67f, 0.92f);
        private static readonly Color StartFillColor = new Color(0.30f, 0.22f, 0.08f, 0.96f);
        private static readonly Color StartBorderColor = new Color(0.96f, 0.78f, 0.34f, 1f);
        private static readonly Color CurrentFillColor = new Color(0.07f, 0.30f, 0.27f, 0.96f);
        private static readonly Color CurrentBorderColor = new Color(0.27f, 0.94f, 0.75f, 1f);

        private readonly Dictionary<string, Label> nodeViews = new Dictionary<string, Label>(StringComparer.Ordinal);
        private readonly List<SceneMapConnection> visibleConnections = new List<SceneMapConnection>();
        private string startSceneName;
        private string currentSceneName;

        public SceneMapGraphView()
        {
            pickingMode = PickingMode.Ignore;
            style.flexGrow = 1f;
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.SpaceEvenly;
            style.alignItems = Align.Stretch;
            style.paddingLeft = 20f;
            style.paddingRight = 20f;
            style.paddingTop = 18f;
            style.paddingBottom = 12f;

            generateVisualContent += DrawConnections;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        public void Populate(SceneMapGraph graph, string activeSceneName)
        {
            Clear();
            nodeViews.Clear();
            visibleConnections.Clear();

            currentSceneName = activeSceneName;
            startSceneName = graph != null ? graph.StartSceneName : SceneMapDataLoader.DefaultStartSceneName;

            List<string> sceneNames = graph != null
                ? graph.GetReachableSceneNames()
                : new List<string>();

            Dictionary<string, int> depths = graph != null
                ? graph.GetReachableDepths()
                : new Dictionary<string, int>(StringComparer.Ordinal);

            if (sceneNames.Count == 0 && !string.IsNullOrWhiteSpace(startSceneName))
            {
                sceneNames.Add(startSceneName);
                depths[startSceneName] = 0;
            }

            AddConnections(graph);
            AddColumns(sceneNames, depths);
            MarkDirtyRepaint();
        }

        private void AddConnections(SceneMapGraph graph)
        {
            if (graph == null)
                return;

            HashSet<string> seenConnections = new HashSet<string>(StringComparer.Ordinal);
            foreach (SceneMapConnection connection in graph.GetReachableConnections())
            {
                string key = GetUndirectedConnectionKey(connection.fromScene, connection.toScene);
                if (!seenConnections.Add(key))
                    continue;

                visibleConnections.Add(connection);
            }
        }

        private void AddColumns(List<string> sceneNames, Dictionary<string, int> depths)
        {
            int columnCount = GetColumnCount(depths);
            List<List<string>> columns = new List<List<string>>();

            for (int i = 0; i < columnCount; i++)
            {
                columns.Add(new List<string>());
            }

            foreach (string sceneName in sceneNames)
            {
                int columnIndex = depths.TryGetValue(sceneName, out int depth)
                    ? Mathf.Clamp(depth, 0, columnCount - 1)
                    : columnCount - 1;

                columns[columnIndex].Add(sceneName);
            }

            for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                VisualElement column = CreateColumn();
                columns[columnIndex].Sort((left, right) =>
                    string.Compare(GetDisplaySceneName(left), GetDisplaySceneName(right), StringComparison.Ordinal));

                foreach (string sceneName in columns[columnIndex])
                {
                    Label node = CreateNode(sceneName);
                    nodeViews[sceneName] = node;
                    column.Add(node);
                }

                Add(column);
            }
        }

        private static VisualElement CreateColumn()
        {
            VisualElement column = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };

            column.style.flexGrow = 1f;
            column.style.flexBasis = 0f;
            column.style.alignItems = Align.Center;
            column.style.justifyContent = Justify.Center;

            return column;
        }

        private Label CreateNode(string sceneName)
        {
            Label node = new Label(GetDisplaySceneName(sceneName))
            {
                pickingMode = PickingMode.Ignore,
                tooltip = sceneName
            };

            bool isStartScene = string.Equals(sceneName, startSceneName, StringComparison.Ordinal);
            bool isCurrentScene = string.Equals(sceneName, currentSceneName, StringComparison.Ordinal);

            node.style.width = 142f;
            node.style.minHeight = 46f;
            node.style.marginTop = 11f;
            node.style.marginBottom = 11f;
            node.style.paddingLeft = 10f;
            node.style.paddingRight = 10f;
            node.style.paddingTop = 8f;
            node.style.paddingBottom = 8f;
            node.style.color = Color.white;
            node.style.fontSize = 17f;
            node.style.unityTextAlign = TextAnchor.MiddleCenter;
            node.style.unityFontStyleAndWeight = isCurrentScene ? FontStyle.Bold : FontStyle.Normal;
            node.style.backgroundColor = isCurrentScene
                ? CurrentFillColor
                : isStartScene
                    ? StartFillColor
                    : NormalFillColor;

            Color borderColor = isCurrentScene
                ? CurrentBorderColor
                : isStartScene
                    ? StartBorderColor
                    : NormalBorderColor;

            node.style.borderTopWidth = 2f;
            node.style.borderRightWidth = 2f;
            node.style.borderBottomWidth = 2f;
            node.style.borderLeftWidth = 2f;
            node.style.borderTopColor = borderColor;
            node.style.borderRightColor = borderColor;
            node.style.borderBottomColor = borderColor;
            node.style.borderLeftColor = borderColor;
            node.style.borderTopLeftRadius = 7f;
            node.style.borderTopRightRadius = 7f;
            node.style.borderBottomLeftRadius = 7f;
            node.style.borderBottomRightRadius = 7f;

            return node;
        }

        private static int GetColumnCount(Dictionary<string, int> depths)
        {
            int maxDepth = 0;
            foreach (int depth in depths.Values)
            {
                maxDepth = Mathf.Max(maxDepth, depth);
            }

            return Mathf.Max(1, maxDepth + 1);
        }

        private static string GetUndirectedConnectionKey(string fromScene, string toScene)
        {
            return string.Compare(fromScene, toScene, StringComparison.Ordinal) <= 0
                ? $"{fromScene}\u001f{toScene}"
                : $"{toScene}\u001f{fromScene}";
        }

        private void DrawConnections(MeshGenerationContext context)
        {
            if (visibleConnections.Count == 0)
                return;

            Painter2D painter = context.painter2D;
            painter.lineWidth = 3f;
            painter.strokeColor = EdgeColor;

            foreach (SceneMapConnection connection in visibleConnections)
            {
                if (!TryGetNodeCenter(connection.fromScene, out Vector2 fromCenter) ||
                    !TryGetNodeCenter(connection.toScene, out Vector2 toCenter))
                {
                    continue;
                }

                painter.BeginPath();
                painter.MoveTo(fromCenter);
                painter.LineTo(toCenter);
                painter.Stroke();
            }
        }

        private bool TryGetNodeCenter(string sceneName, out Vector2 center)
        {
            center = Vector2.zero;

            if (!nodeViews.TryGetValue(sceneName, out Label node) || node.panel == null)
                return false;

            Rect nodeBounds = node.worldBound;
            Rect graphBounds = worldBound;
            center = new Vector2(
                nodeBounds.x + (nodeBounds.width * 0.5f) - graphBounds.x,
                nodeBounds.y + (nodeBounds.height * 0.5f) - graphBounds.y
            );
            return true;
        }
    }
}
