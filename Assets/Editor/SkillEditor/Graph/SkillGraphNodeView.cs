using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace GSkill.SkillEditor.Graph
{
    /// <summary>
    /// GraphView 节点视图
    /// </summary>
    public sealed class SkillGraphNodeView : Node
    {
        private readonly Dictionary<SkillGraphPortCategory, Port> _inputPorts = new();
        private readonly Dictionary<SkillGraphPortCategory, Port> _outputPorts = new();
        private readonly Label _subtitleLabel;
        private readonly VisualElement _detailContainer;
        private readonly bool _showDetail;

        public SkillGraphNodeView(SkillGraphNode node, bool showDetail)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            _showDetail = showDetail;

            title = string.IsNullOrWhiteSpace(node.Title) ? "节点" : node.Title;
            tooltip = node.Description;

            _subtitleLabel = new Label(node.Subtitle ?? string.Empty)
            {
                tooltip = node.Description
            };
            _subtitleLabel.AddToClassList("gskill-node-subtitle");
            titleContainer.Add(_subtitleLabel);

            switch (node.NodeType)
            {
                case SkillGraphNodeType.Skill:
                case SkillGraphNodeType.Buff:
                    AddInputPort(SkillGraphPortCategory.Default, "关联", Port.Capacity.Multi);
                    AddOutputPort(SkillGraphPortCategory.SequenceOutput, "动作", Port.Capacity.Multi);
                    AddOutputPort(SkillGraphPortCategory.TriggerOutput, "触发", Port.Capacity.Multi);
                    break;
                case SkillGraphNodeType.Sequence:
                case SkillGraphNodeType.Timeline:
                    AddInputPort(SkillGraphPortCategory.Default, "前驱", Port.Capacity.Multi);
                    AddOutputPort(SkillGraphPortCategory.TimelineEffectOutput, "效果", Port.Capacity.Multi);
                    capabilities &= ~Capabilities.Collapsible;
                    break;
                case SkillGraphNodeType.Trigger:
                    AddInputPort(SkillGraphPortCategory.Default, "绑定", Port.Capacity.Multi);
                    AddOutputPort(SkillGraphPortCategory.TriggerActionOutput, "动作", Port.Capacity.Multi);
                    break;
                case SkillGraphNodeType.Effect:
                    AddInputPort(SkillGraphPortCategory.Default, "触发", Port.Capacity.Multi);
                    break;
                case SkillGraphNodeType.Placeholder:
                    AddInputPort(SkillGraphPortCategory.Default, "引用", Port.Capacity.Multi);
                    break;
            }

            if (node.IsMissingReference)
            {
                mainContainer.style.backgroundColor = new Color(0.36f, 0.16f, 0.16f, 0.95f);
            }
            else
            {
                mainContainer.style.backgroundColor = new Color(0.17f, 0.2f, 0.23f, 0.95f);
            }

            _detailContainer = new VisualElement
            {
                style =
                {
                    marginTop = 4f,
                    flexDirection = FlexDirection.Column
                }
            };
            mainContainer.Add(_detailContainer);

            BuildDetailContents();

            RefreshExpandedState();
            RefreshPorts();
        }

        public event Action<SkillGraphNode> NodeSelected;
        public event Action<int, int, int> TimelineSlotChanged;

        public SkillGraphNode Node { get; }

        public override void OnSelected()
        {
            base.OnSelected();
            NodeSelected?.Invoke(Node);
        }

        public Port GetPort(SkillGraphPortCategory category, Direction direction)
        {
            var dict = direction == Direction.Input ? _inputPorts : _outputPorts;
            if (dict.TryGetValue(category, out var port))
            {
                return port;
            }

            return dict.TryGetValue(SkillGraphPortCategory.Default, out var fallback) ? fallback : null;
        }

        public void UpdateSubtitle(string subtitle)
        {
            _subtitleLabel.text = subtitle ?? string.Empty;
        }

        private void BuildDetailContents()
        {
            _detailContainer.Clear();

            if (!_showDetail)
            {
                if (Node.NodeType == SkillGraphNodeType.Timeline || Node.NodeType == SkillGraphNodeType.Sequence)
                {
                    var summary = new Label(Node.TimelineSlots.Count > 0
                        ? $"关键帧: {Node.TimelineSlots.Count}"
                        : "暂无关键帧")
                    {
                        tooltip = Node.Subtitle
                    };
                    summary.AddToClassList("gskill-node-detail");
                    _detailContainer.Add(summary);
                }
                return;
            }

            if (Node.NodeType == SkillGraphNodeType.Timeline || Node.NodeType == SkillGraphNodeType.Sequence)
            {
                BuildTimelineControls();
                return;
            }

            if (Node.DetailLines.Count == 0)
            {
                return;
            }

            foreach (var detail in Node.DetailLines)
            {
                var label = new Label(detail)
                {
                    tooltip = detail
                };
                label.AddToClassList("gskill-node-detail");
                _detailContainer.Add(label);
            }
        }

        private void BuildTimelineControls()
        {
            var slots = Node.TimelineSlots;
            if (slots.Count == 0)
            {
                _detailContainer.Add(new Label("尚未绑定效果"));
                return;
            }

            var maxDelay = Mathf.Max(5000, slots.Max(s => s.Delay) + 1000);
            var sequenceId = Node.Reference.SequenceId.GetValueOrDefault();

            foreach (var slot in slots)
            {
                var slotContainer = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        marginBottom = 4f
                    }
                };

                var titleLabel = new Label($"槽 {slot.SlotIndex + 1}: {slot.EffectTitle} ({slot.Delay}ms)")
                {
                    tooltip = slot.Summary
                };
                titleLabel.AddToClassList("gskill-timeline-slot-title");
                slotContainer.Add(titleLabel);

                var slider = new SliderInt(0, maxDelay)
                {
                    label = "延迟",
                    value = Mathf.Clamp(slot.Delay, 0, maxDelay)
                };
                slider.style.marginTop = 2f;
                slider.RegisterValueChangedCallback(evt =>
                {
                    var newDelay = Mathf.Max(0, evt.newValue);
                    slider.SetValueWithoutNotify(newDelay);
                    titleLabel.text = $"槽 {slot.SlotIndex + 1}: {slot.EffectTitle} ({newDelay}ms)";
                    TimelineSlotChanged?.Invoke(sequenceId, slot.SlotIndex, newDelay);
                });
                slotContainer.Add(slider);

                if (!string.IsNullOrEmpty(slot.Summary))
                {
                    var summaryLabel = new Label(slot.Summary)
                    {
                        style = { unityFontStyleAndWeight = FontStyle.Italic }
                    };
                    summaryLabel.AddToClassList("gskill-node-detail");
                    slotContainer.Add(summaryLabel);
                }

                _detailContainer.Add(slotContainer);
            }
        }

        private void AddInputPort(SkillGraphPortCategory category, string portName, Port.Capacity capacity)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Input, capacity, typeof(int));
            port.portName = portName;
            _inputPorts[category] = port;
            inputContainer.Add(port);
        }

        private void AddOutputPort(SkillGraphPortCategory category, string portName, Port.Capacity capacity)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, capacity, typeof(int));
            port.portName = portName;
            _outputPorts[category] = port;
            outputContainer.Add(port);
        }
    }
}

