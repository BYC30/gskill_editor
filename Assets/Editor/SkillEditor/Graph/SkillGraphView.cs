using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace GSkill.SkillEditor.Graph
{
    /// <summary>
    /// 技能蓝图 GraphView
    /// </summary>
    public sealed class SkillGraphView : GraphView
    {
        private readonly Dictionary<string, SkillGraphNodeView> _nodeViews = new();
        private bool _showDetail;

        public SkillGraphView()
        {
            style.flexGrow = 1f;
            this.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
        }

        public event Action<SkillGraphNode> NodeSelected;
        public event Action<int, int, int> TimelineSlotChanged;

        public void BuildGraph(SkillGraph graph, bool showDetail)
        {
            _showDetail = showDetail;
            ClearGraph();
            if (graph == null)
            {
                return;
            }

            foreach (var node in graph.Nodes)
            {
                var view = new SkillGraphNodeView(node, showDetail);
                view.NodeSelected += HandleNodeSelected;
                view.TimelineSlotChanged += HandleTimelineSlotChanged;
                view.SetPosition(new Rect(Vector2.zero, new Vector2(240f, 150f)));
                _nodeViews[node.Id] = view;
                AddElement(view);
            }

            foreach (var edge in graph.Edges)
            {
                if (!_nodeViews.TryGetValue(edge.FromNodeId, out var fromNode))
                {
                    continue;
                }

                if (!_nodeViews.TryGetValue(edge.ToNodeId, out var toNode))
                {
                    continue;
                }

                var fromPort = fromNode.GetPort(edge.FromPort, Direction.Output);
                var toPort = toNode.GetPort(edge.ToPort, Direction.Input);
                if (fromPort == null || toPort == null)
                {
                    continue;
                }

                var edgeView = fromPort.ConnectTo(toPort);
                AddElement(edgeView);
            }

            AutoLayout(graph);
            ScheduleFrameAll();
        }

        public void ClearGraph()
        {
            foreach (var view in _nodeViews.Values)
            {
                view.NodeSelected -= HandleNodeSelected;
                view.TimelineSlotChanged -= HandleTimelineSlotChanged;
            }

            _nodeViews.Clear();
            ClearSelection();

            var elements = graphElements.ToList();
            if (elements.Count > 0)
            {
                DeleteElements(elements);
            }
        }

        public void FocusNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            if (_nodeViews.TryGetValue(nodeId, out var view))
            {
                ClearSelection();
                AddToSelection(view);
                FrameSelection();
            }
        }

        private void HandleNodeSelected(SkillGraphNode node)
        {
            NodeSelected?.Invoke(node);
        }

        private void HandleTimelineSlotChanged(int sequenceId, int slotIndex, int delay)
        {
            if (_nodeViews.TryGetValue($"effect:{sequenceId}:{slotIndex}", out var effectNode))
            {
                effectNode.UpdateSubtitle($"延迟 {delay}ms");
            }

            TimelineSlotChanged?.Invoke(sequenceId, slotIndex, delay);
        }

        private void AutoLayout(SkillGraph graph)
        {
            const float nodeWidth = 240f;
            const float nodeHeight = 150f;
            const float xSpacing = 220f;
            const float ySpacing = 120f;

            var columns = new Dictionary<string, List<SkillGraphNode>>
            {
                { "root", new List<SkillGraphNode>() },
                { "trigger", new List<SkillGraphNode>() },
                { "timeline", new List<SkillGraphNode>() },
                { "effect", new List<SkillGraphNode>() },
                { "other", new List<SkillGraphNode>() }
            };

            foreach (var node in graph.Nodes)
            {
                var key = GetColumnKey(node);
                if (!columns.TryGetValue(key, out var list))
                {
                    list = columns["other"];
                }

                list.Add(node);
            }

            foreach (var pair in columns)
            {
                var columnNodes = pair.Value;
                var count = columnNodes.Count;
                if (count == 0)
                {
                    continue;
                }

                for (var index = 0; index < count; index++)
                {
                    if (!_nodeViews.TryGetValue(columnNodes[index].Id, out var view))
                    {
                        continue;
                    }

                    var x = GetColumnOffset(pair.Key) * xSpacing;
                    var y = (index - (count - 1) / 2f) * ySpacing;
                    var rect = new Rect(new Vector2(x - nodeWidth / 2f, y - nodeHeight / 2f), new Vector2(nodeWidth, nodeHeight));
                    view.SetPosition(rect);
                }
            }
        }

        private static string GetColumnKey(SkillGraphNode node)
        {
            switch (node.NodeType)
            {
                case SkillGraphNodeType.Skill:
                case SkillGraphNodeType.Buff:
                    return "root";
                case SkillGraphNodeType.Trigger:
                    return "trigger";
                case SkillGraphNodeType.Sequence:
                case SkillGraphNodeType.Timeline:
                    return "timeline";
                case SkillGraphNodeType.Effect:
                    return "effect";
                case SkillGraphNodeType.Placeholder:
                    return node.Reference.NodeType switch
                    {
                        SkillGraphNodeType.Trigger => "trigger",
                        SkillGraphNodeType.Sequence => "timeline",
                        SkillGraphNodeType.Timeline => "timeline",
                        SkillGraphNodeType.Effect => "effect",
                        _ => "other"
                    };
                default:
                    return "other";
            }
        }

        private static float GetColumnOffset(string key)
        {
            return key switch
            {
                "root" => -1.5f,
                "trigger" => -0.5f,
                "timeline" => 0.8f,
                "effect" => 2.1f,
                _ => 2.7f
            };
        }

        private void ScheduleFrameAll()
        {
            if (_nodeViews.Count == 0)
            {
                return;
            }

            schedule.Execute((TimerState _) => FrameAll()).StartingIn(30);
        }
    }
}
