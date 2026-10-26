using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SphereVoronoiPrototype
{
    // UI layer for the authored settings panel. It owns the widget references
    // and label formatting, but knows nothing about mesh generation.
    public class SphereVoronoiSettingsUI : MonoBehaviour
    {
        [Header("Controls")]
        public Slider cellsSlider;
        public RectTransform seedInputParent;
        public TMP_InputField seedInput;
        public Button regenerateButton;
        public Button resetButton;

        [Header("Labels")]
        public TextMeshProUGUI cellsValue;
        public TextMeshProUGUI statsText;
        public TextMeshProUGUI selectionText;

        public event Action RegenerateRequested;
        public event Action ResetRequested;

        private bool _eventsHooked;

        public bool IsReady =>
            cellsSlider != null
            && seedInput != null
            && regenerateButton != null && resetButton != null
            && cellsValue != null
            && statsText != null
            && selectionText != null;

        public int Cells => Mathf.RoundToInt(cellsSlider.value);

        public bool TryGetSeed(out int seed)
        {
            return int.TryParse(seedInput.text, out seed);
        }

        private void Awake()
        {
            if (seedInput == null)
                seedInput = CreateSeedInput();

            if (!IsReady)
            {
                Debug.LogError("[SphereVoronoiSettingsUI] Panel references are missing. ", this);
                return;
            }

            HookEvents();
            RefreshLabels();
        }

        private void OnDestroy()
        {
            UnhookEvents();
        }

        public void SetValues(int cells)
        {
            cellsSlider.SetValueWithoutNotify(cells);
            RefreshLabels();
        }

        public void SetDefaultValues()
        {
            SetValues(120);
            seedInput.SetTextWithoutNotify(string.Empty);
        }

        public void SetStats(string text)
        {
            statsText.text = text;
        }

        public void SetSelection(string text)
        {
            selectionText.text = text;
        }

        private void HookEvents()
        {
            if (_eventsHooked)
                return;

            cellsSlider.onValueChanged.AddListener(OnValueChanged);
            regenerateButton.onClick.AddListener(OnRegenerateClicked);
            resetButton.onClick.AddListener(OnResetClicked);
            _eventsHooked = true;
        }

        private void UnhookEvents()
        {
            if (!_eventsHooked)
                return;

            cellsSlider.onValueChanged.RemoveListener(OnValueChanged);
            regenerateButton.onClick.RemoveListener(OnRegenerateClicked);
            resetButton.onClick.RemoveListener(OnResetClicked);
            _eventsHooked = false;
        }

        private void OnValueChanged(float value)
        {
            RefreshLabels();
        }

        private void OnRegenerateClicked()
        {
            RegenerateRequested?.Invoke();
        }

        private void OnResetClicked()
        {
            ResetRequested?.Invoke();
        }

        private void RefreshLabels()
        {
            if (cellsValue != null)
                cellsValue.text = Cells.ToString();
        }

        private TMP_InputField CreateSeedInput()
        {
            if (seedInputParent == null || cellsValue == null || cellsValue.font == null)
                return null;

            var root = new GameObject("Seed Input", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(RectMask2D));
            root.transform.SetParent(seedInputParent, false);
            var inputRect = (RectTransform)root.transform;
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = new Vector2(0f, 8f);
            inputRect.offsetMax = new Vector2(0f, -32f);
            var image = root.GetComponent<Image>();
            image.color = new Color(0.13f, 0.14f, 0.18f, 1f);

            TMP_Text CreateText(string name, string value, Color color)
            {
                var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var rect = (RectTransform)textObject.transform;
                rect.SetParent(root.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(8f, 2f);
                rect.offsetMax = new Vector2(-8f, -2f);

                var text = textObject.GetComponent<TextMeshProUGUI>();
                text.font = cellsValue.font;
                text.fontSize = cellsValue.fontSize;
                text.color = color;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
                text.text = value;
                return text;
            }

            TMP_Text valueText = CreateText("Text", string.Empty, Color.white);
            TMP_Text placeholder = CreateText("Placeholder", "Empty = random", new Color(0.65f, 0.66f, 0.7f, 1f));
            var input = root.AddComponent<TMP_InputField>();
            input.textViewport = (RectTransform)root.transform;
            input.textComponent = valueText;
            input.placeholder = placeholder;
            input.targetGraphic = image;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 11;
            return input;
        }
    }
}
