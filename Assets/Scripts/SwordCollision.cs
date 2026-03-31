using UnityEngine;

public class SwordCollision : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask enemyLayer; // Слой врага
    [SerializeField] private bool debugMode = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем, коснулись ли мы врага
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            Debug.Log("Меч коснулся врага!");

            // Сообщаем GameManager'у, что было касание
            GameManager.Instance.PlayerHitEnemy();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Если используешь Collision вместо Trigger
        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            Debug.Log("Меч коснулся врага (коллизия)!");
            GameManager.Instance.PlayerHitEnemy();
        }
    }
}