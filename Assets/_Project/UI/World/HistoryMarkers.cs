using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Short-lived map callouts for locations opened from the history report.
    public sealed class HistoryMarkers
    {
        const float VisibleSeconds = 5f;

        readonly UiContext _ui;
        readonly VisualElement _marker;
        readonly Label _label;
        float2 _position;
        float _expiresAt;
        bool _active;

        public HistoryMarkers(UiContext ui, VisualElement parent)
        {
            _ui = ui;
            _marker = new VisualElement { pickingMode = PickingMode.Ignore };
            _marker.AddToClassList("pg-history-marker");
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList("pg-history-marker__label");
            _marker.Add(_label);
            parent.Insert(1, _marker);
            Hide();
        }

        public void Show(float2 position, string label)
        {
            _position = position;
            _label.text = label;
            _expiresAt = Time.unscaledTime + VisibleSeconds;
            _active = true;
            _marker.style.display = DisplayStyle.Flex;
        }

        public void Update()
        {
            if (!_active || Time.unscaledTime >= _expiresAt || _marker.panel == null)
            {
                Hide();
                return;
            }

            var camera = _ui.Camera != null ? _ui.Camera.Camera : null;
            if (camera == null)
            {
                Hide();
                return;
            }

            var screen = camera.WorldToScreenPoint(new Vector3(_position.x, _position.y, 0f));
            if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height)
            {
                _marker.style.display = DisplayStyle.None;
                return;
            }

            var panelPosition = RuntimePanelUtils.ScreenToPanel(_marker.panel, new Vector2(screen.x, Screen.height - screen.y));
            _marker.style.left = panelPosition.x - 10f;
            _marker.style.top = panelPosition.y - 10f;
            _marker.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _active = false;
            _marker.style.display = DisplayStyle.None;
        }
    }
}
