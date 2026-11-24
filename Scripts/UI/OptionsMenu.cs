using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poeirinha
{
    /// <summary>
    /// Tela de opções (aberta pelo menu principal e pela pausa). Só lê e grava GameSettings.
    /// </summary>
    public class OptionsMenu : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] GameObject firstSelected;

        [Header("Controles")]
        [SerializeField] Slider turnSlider;
        [SerializeField] TMP_Text turnValue;
        [SerializeField] Slider mouseSlider;
        [SerializeField] TMP_Text mouseValue;
        [SerializeField] Toggle invertYToggle;
        [SerializeField] Toggle recenterToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeValue;

        GameObject returnPanel;
        GameObject returnSelected;
        bool refreshing;

        public bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            panel.SetActive(false);
        }

        /// <summary>Abre as opções escondendo o painel de origem (que volta ao fechar).</summary>
        public void Open(GameObject fromPanel, GameObject fromSelected)
        {
            returnPanel = fromPanel;
            returnSelected = fromSelected;
            if (returnPanel != null) returnPanel.SetActive(false);
            panel.SetActive(true);
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            if (returnPanel != null) returnPanel.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(returnSelected);
        }

        /// <summary>Fecha sem reabrir o painel de origem (ex.: despausou com Esc).</summary>
        public void HideImmediate() => panel.SetActive(false);

        void Refresh()
        {
            refreshing = true; // evita que mexer no slider por código grave de volta
            turnSlider.minValue = GameSettings.MinTurn;
            turnSlider.maxValue = GameSettings.MaxTurn;
            turnSlider.value = GameSettings.TurnSensitivity;
            mouseSlider.minValue = GameSettings.MinMouse;
            mouseSlider.maxValue = GameSettings.MaxMouse;
            mouseSlider.value = GameSettings.MouseSensitivity;
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = GameSettings.MasterVolume;
            invertYToggle.isOn = GameSettings.InvertY;
            recenterToggle.isOn = GameSettings.AutoRecenter;
            refreshing = false;
            UpdateLabels();
        }

        void UpdateLabels()
        {
            turnValue.text = $"{GameSettings.TurnSensitivity:0.0}x";
            mouseValue.text = $"{GameSettings.MouseSensitivity:0.0}x";
            volumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";
        }

        // Ligados aos componentes no Inspector (OnValueChanged / OnClick).
        public void OnTurnChanged(float v) { if (refreshing) return; GameSettings.TurnSensitivity = v; UpdateLabels(); }
        public void OnMouseChanged(float v) { if (refreshing) return; GameSettings.MouseSensitivity = v; UpdateLabels(); }
        public void OnVolumeChanged(float v) { if (refreshing) return; GameSettings.MasterVolume = v; UpdateLabels(); }
        public void OnInvertYChanged(bool v) { if (refreshing) return; GameSettings.InvertY = v; }
        public void OnRecenterChanged(bool v) { if (refreshing) return; GameSettings.AutoRecenter = v; }
        public void OnResetClicked() { GameSettings.ResetToDefaults(); Refresh(); }
        public void OnBackClicked() => Close();
    }
}
