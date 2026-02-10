using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Transform target;
    public float distance = 8f;
    public float pitch = 35f;
    public float yaw = -35f;

    [Tooltip("Degrees per pixel")]
    public float rotateSpeed = 0.25f;
    public float zoomSpeed = 2.5f;

    Vector3 lastMousePos;
    bool dragging;

    void LateUpdate()
    {
        if (target == null) return;

        // 更好用的触发方式（满足其一即可旋转）：
        // 1) 按住 Space + 左键拖动（如果你真的能按住左键）
        // 2) 右键拖动（Mac 触控板两指按住更稳定）
        // 3) Option/Alt + 左键拖动（非常 Mac）
        bool orbitHeld =
            (Input.GetKey(KeyCode.Space) && Input.GetMouseButton(0)) ||
            Input.GetMouseButton(1) ||
            ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetMouseButton(0));

        if (orbitHeld)
        {
            if (!dragging)
            {
                dragging = true;
                lastMousePos = Input.mousePosition;
            }
            else
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                lastMousePos = Input.mousePosition;

                yaw   += delta.x * rotateSpeed;
                pitch -= delta.y * rotateSpeed;
                pitch = Mathf.Clamp(pitch, 15f, 80f);
            }
        }
        else
        {
            dragging = false;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
            distance = Mathf.Clamp(distance - scroll * zoomSpeed * 0.1f, 4f, 16f);

        var rot = Quaternion.Euler(pitch, yaw, 0);
        transform.position = target.position + rot * new Vector3(0, 0, -distance);
        transform.LookAt(target.position);
    }
}