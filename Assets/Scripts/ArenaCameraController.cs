using UnityEngine;

public class ArenaCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform swordTarget;    // Меч за которым следим
    [SerializeField] private float smoothSpeed = 5f;   // Скорость следования
    [SerializeField] private float arenaWidth = 20f;    // Ширина арены

    private Camera cam;
    private float halfCameraWidth;
    private float fixedY;
    private float fixedZ;

    void Start()
    {
        cam = GetComponent<Camera>();

        // Запоминаем начальные координаты
        fixedY = transform.position.y;
        fixedZ = transform.position.z;

        UpdateCameraWidth();
    }

    void UpdateCameraWidth()
    {
        float cameraHeight = cam.orthographicSize * 2;
        float aspectRatio = (float)Screen.width / Screen.height;
        halfCameraWidth = cameraHeight * aspectRatio / 2;
    }

    void LateUpdate()
    {
        if (swordTarget == null) return;

        // 1. ЖЕСТКО ФИКСИРУЕМ ВСЁ КРОМЕ X
        transform.rotation = Quaternion.identity;

        // 2. Получаем позицию меча по X
        float targetX = swordTarget.position.x;

        // 3. Ограничиваем
        float minX = -arenaWidth / 2 + halfCameraWidth;
        float maxX = arenaWidth / 2 - halfCameraWidth;

        targetX = Mathf.Clamp(targetX, minX, maxX);

        // 4. Новая позиция
        Vector3 desiredPosition = new Vector3(targetX, fixedY, fixedZ);

        // 5. Плавное движение
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}