using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Синглтон
    public static GameManager Instance { get; private set; }

    [Header("Round Settings")]
    [SerializeField] private Transform playerStartPosition; // Начальная позиция игрока
    [SerializeField] private Transform enemyStartPosition;  // Начальная позиция врага
    [SerializeField] private GameObject playerSword;        // Меч игрока
    [SerializeField] private GameObject enemySword;         // Меч врага

    [Header("Health")]
    [SerializeField] private int playerHealth = 5;
    [SerializeField] private int enemyHealth = 5;
    [SerializeField] private int maxHealth = 5;

    private void Awake()
    {
        // Синглтон
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Не уничтожать при смене сцены
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Если стартовые позиции не заданы, создаем их
        if (playerStartPosition == null)
            CreateStartPositions();

        ResetRound();
    }

    void CreateStartPositions()
    {
        // Создаем пустые объекты для позиций
        GameObject playerStart = new GameObject("PlayerStart");
        playerStart.transform.position = new Vector3(-5, 0, 0); // Левая часть арены

        GameObject enemyStart = new GameObject("EnemyStart");
        enemyStart.transform.position = new Vector3(5, 0, 0); // Правая часть арены

        playerStartPosition = playerStart.transform;
        enemyStartPosition = enemyStart.transform;
    }

    // Вызывается когда игрок попал по врагу
    public void PlayerHitEnemy()
    {
        Debug.Log("Игрок попал по врагу!");

        // Уменьшаем здоровье врага
        enemyHealth--;

        if (enemyHealth <= 0)
        {
            // Враг умер
            Debug.Log("Враг побежден!");
            ResetGame();
        }
        else
        {
            // Начинаем новый раунд
            ResetRound();
        }
    }

    // Вызывается когда враг попал по игроку (добавим позже)
    public void EnemyHitPlayer()
    {
        Debug.Log("Враг попал по игроку!");

        playerHealth--;

        if (playerHealth <= 0)
        {
            Debug.Log("Игрок проиграл!");
            ResetGame();
        }
        else
        {
            ResetRound();
        }
    }

    // Сброс раунда (возврат на стартовые позиции)
    void ResetRound()
    {
        Debug.Log($"Новый раунд! Здоровье: Игрок {playerHealth}, Враг {enemyHealth}");

        // Возвращаем игрока на стартовую позицию
        if (playerSword != null && playerStartPosition != null)
        {
            Rigidbody2D rb = playerSword.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; // Останавливаем движение
                rb.angularVelocity = 0f; // Останавливаем вращение
            }
            playerSword.transform.position = playerStartPosition.position;
            playerSword.transform.rotation = Quaternion.identity;
        }

        // Возвращаем врага на стартовую позицию
        if (enemySword != null && enemyStartPosition != null)
        {
            Rigidbody2D rb = enemySword.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            enemySword.transform.position = enemyStartPosition.position;
            enemySword.transform.rotation = Quaternion.identity;
        }
    }

    // Полный сброс игры (кто-то умер)
    void ResetGame()
    {
        Debug.Log("Игра окончена! Рестарт...");

        // Сбрасываем здоровье
        playerHealth = maxHealth;
        enemyHealth = maxHealth;

        // Сбрасываем раунд
        ResetRound();
    }

    // Методы для получения здоровья (для UI)
    public int GetPlayerHealth() => playerHealth;
    public int GetEnemyHealth() => enemyHealth;
    public int GetMaxHealth() => maxHealth;
}