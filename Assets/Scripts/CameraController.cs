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
    public float minPitch = 5.0f;
    public float maxPitch = 60.0f;

    [Header("벽 충돌 설정")]
    public float minDistance     = 0.5f;  // 벽에 붙었을 때 최소 거리
    public float collisionRadius = 0.2f;  // SphereCast 구 반경

    private float currentYaw   = 0.0f;
    private float currentPitch = 20.0f;
    private int   mCollisionMask;

    void Start()
    {
        if (target == null)
            Debug.LogError("카메라의 Target(공)을 지정해주세요!");

        // GolfBall 레이어는 충돌 대상에서 제외 (공 자체에 막히지 않게)
        mCollisionMask = ~LayerMask.GetMask("GolfBall", "Ignore Raycast");
    }

    void LateUpdate()
    {
        if (target == null) return;

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput   = Input.GetAxis("Vertical");

        currentYaw   += horizontalInput * yawSpeed   * Time.deltaTime;
        currentPitch -= verticalInput   * pitchSpeed * Time.deltaTime;
        currentPitch  = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        Quaternion rotation  = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3    direction = rotation * Vector3.back; // 공 기준 카메라 방향

        // SphereCast: 공 → 카메라 방향으로 장애물 감지
        float actualDistance = distance;
        if (Physics.SphereCast(target.position, collisionRadius, direction,
                               out RaycastHit hit, distance, mCollisionMask))
        {
            actualDistance = Mathf.Max(hit.distance - collisionRadius, minDistance);
        }

        transform.position = rotation * new Vector3(0f, 0f, -actualDistance) + target.position;
        transform.rotation = rotation;
    }

    public Vector3 GetLookDirection()
    {
        Vector3 forward = transform.forward;
        forward.y = 0;
        return forward.normalized;
    }

    // 발사각 반환: 카메라가 수평(낮은 pitch) → 높은 발사각, 아래를 볼수록 → 낮은 발사각
    public float GetPitchAngle()
    {
        return minPitch + maxPitch - currentPitch;
    }
}