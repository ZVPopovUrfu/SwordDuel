using UnityEngine;
using TMPro; // Обязательно добавить эту строку!

public class HealthUI : MonoBehaviour
{
    [Header("Text References")]
    [SerializeField] private TextMeshProUGUI playerHealthText; // Изменили на TMP
    [SerializeField] private TextMeshProUGUI enemyHealthText;  // Изменили на TMP
    [SerializeField] private TextMeshProUGUI roundText;        // Изменили на TMP

    void Update()
    {
        if (GameManager.Instance == null) return;

        if (playerHealthText != null)
            playerHealthText.text = $"Player: {GameManager.Instance.GetPlayerHealth()}";

        if (enemyHealthText != null)
            enemyHealthText.text = $"Enemy: {GameManager.Instance.GetEnemyHealth()}";

        if (roundText != null)
            roundText.text = $"Health: {GameManager.Instance.GetPlayerHealth()} / {GameManager.Instance.GetEnemyHealth()}";
    }
}