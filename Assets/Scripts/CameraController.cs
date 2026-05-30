using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target Info")]
    public Transform target; // 골프 공의 Transform
    public float distance = 10.0f; // 공과의 거리

    [Header("Rotation Speed")]
    public float yawSpeed = 100.0f;   // 좌우 회전 속도
    public float pitchSpeed = 80.0f;  // 위아래 회전 속도

    [Header("Pitch Limits")]
    public float minPitch = 5.0f;    // 너무 땅 아래로 내려가지 않게 제한
    public float maxPitch = 60.0f;   // 너무 머리 위로 올라가지 않게 제한

    private float currentYaw = 0.0f;
    private float currentPitch = 20.0f; // 초기 위아래 각도

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("카메라의 Target(공)을 지정해주세요!");
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Horizontal: A/D, Left/Right | Vertical: W/S, Up/Down
        float horizontalInput = Input.GetAxis("Horizontal"); 
        float verticalInput = Input.GetAxis("Vertical");

        // 입력값에 따라 각도 누적
        currentYaw += horizontalInput * yawSpeed * Time.deltaTime;
        currentPitch -= verticalInput * pitchSpeed * Time.deltaTime; 

        // 위아래 회전각 제한 (카메라 뒤집힘 방지)
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // 구면 좌표계 변환 및 위치 계산
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + target.position;

        // 카메라에 값 적용
        transform.rotation = rotation;
        transform.position = position;
    }

    public Vector3 GetLookDirection()
    {
        Vector3 forward = transform.forward;
        forward.y = 0;
        return forward.normalized;
    }
}