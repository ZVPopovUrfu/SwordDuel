using UnityEngine;

public class EnemySwordCollision : MonoBehaviour
{
    [SerializeField] private LayerMask playerLayer;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            Debug.Log("Враг попал по игроку!");
            GameManager.Instance.EnemyHitPlayer();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Если используешь Collision вместо Trigger
        if (((1 << collision.gameObject.layer) & playerLayer) != 0)
        {
            Debug.Log("Враг попал по игроку!");
            GameManager.Instance.EnemyHitPlayer();
        }
    }
}