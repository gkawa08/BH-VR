using UnityEngine;
using UnityEngine.EventSystems; // ★これが必要です

public class SimpleCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 50.0f;
    public float xSpeed = 120.0f;
    public float ySpeed = 120.0f;
    public float scrollSensitivity = 100.0f;

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        var angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        if (target == null)
        {
            GameObject centerObj = new GameObject("CameraCenter");
            target = centerObj.transform;
        }
    }

    void LateUpdate()
    {
        if (target)
        {
            // --- 回転操作 (右クリック) ---
            // UIの上でクリックし始めた場合は回転させない等の制御も可能ですが、
            // 今回はズームの干渉を防ぐことが主目的なので、回転はそのまま許可します。
            if (Input.GetMouseButton(1))
            {
                x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
            }

            // --- ズーム操作 (ホイール) ---
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            // ★追加部分: マウスがUIの上にある場合は、スクロール値を0にしてズームさせない
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                scroll = 0.0f;
            }

            distance -= scroll * scrollSensitivity;
            distance = Mathf.Clamp(distance, 1f, 500f);

            // --- 位置・回転の適用 ---
            y = ClampAngle(y, -80, 80);

            Quaternion rotation = Quaternion.Euler(y, x, 0);
            Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.position;

            transform.rotation = rotation;
            transform.position = position;
        }
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}