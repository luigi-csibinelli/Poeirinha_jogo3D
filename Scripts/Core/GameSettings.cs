using System;
using UnityEngine;

namespace Poeirinha
{
    /// <summary>
    /// Opções do jogador (tela "Opções"), salvas no PlayerPrefs para durarem entre partidas.
    /// Os sistemas leem os valores daqui; a tela de opções só escreve.
    /// </summary>
    public static class GameSettings
    {
        public const float MinTurn = 0.4f, MaxTurn = 1.6f;
        public const float MinMouse = 0.2f, MaxMouse = 2.5f;

        const string KeyTurn = "poeirinha.turnSensitivity";
        const string KeyMouse = "poeirinha.mouseSensitivity";
        const string KeyInvertY = "poeirinha.invertY";
        const string KeyRecenter = "poeirinha.autoRecenter";
        const string KeyVolume = "poeirinha.masterVolume";

        /// <summary>Disparado quando qualquer opção muda.</summary>
        public static event Action OnChanged;

        /// <summary>Multiplicador da velocidade de giro do robô (A/D). 1 = padrão.</summary>
        public static float TurnSensitivity
        {
            get => PlayerPrefs.GetFloat(KeyTurn, 1f);
            set => SetFloat(KeyTurn, Mathf.Clamp(value, MinTurn, MaxTurn));
        }

        /// <summary>Multiplicador da sensibilidade do mouse na câmera. 1 = padrão.</summary>
        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(KeyMouse, 1f);
            set => SetFloat(KeyMouse, Mathf.Clamp(value, MinMouse, MaxMouse));
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt(KeyInvertY, 0) == 1;
            set => SetInt(KeyInvertY, value ? 1 : 0);
        }

        /// <summary>A câmera volta sozinha para trás do robô quando o mouse fica parado.</summary>
        public static bool AutoRecenter
        {
            get => PlayerPrefs.GetInt(KeyRecenter, 1) == 1;
            set => SetInt(KeyRecenter, value ? 1 : 0);
        }

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            set => SetFloat(KeyVolume, Mathf.Clamp01(value));
        }

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(KeyTurn);
            PlayerPrefs.DeleteKey(KeyMouse);
            PlayerPrefs.DeleteKey(KeyInvertY);
            PlayerPrefs.DeleteKey(KeyRecenter);
            PlayerPrefs.DeleteKey(KeyVolume);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
