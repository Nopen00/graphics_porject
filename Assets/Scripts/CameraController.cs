using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target Info")]
    public Transform target;
    public float distance = 10.0f;

    [Header("Rotation Speed")]
    public float yawSpeed   = 100.0f;
    public float pitchSpeed = 80.0f;

    [Header("Pitch Limits")]
    public float minPitch = 5.0f;
    public float maxPitch = 60.0f;

    [Header("벽 충돌 설정")]
    public float minDistance     = 0.5f;
    public float collisionRadius = 0.2f;

    [Header("단계별 카메라 설정")]
    [SerializeField] private float mAimSideOffset  = 35f;  // 조준 단계: 옆에서 보는 각도 오프셋
    [SerializeField] private float mChargePitch    = 15f;  // 파워 단계: 카메라 수직 각도
    [SerializeField] private float mTransitionSpeed = 5f;  // 단계 전환 보간 속도

    // 조준 방향 (발사 계산에 사용)
    private float mAimYaw   = 0f;
    private float mAimPitch = 40f;

    // 실제 카메라 위치 yaw (보간 중인 값)
    private float mCameraYaw;

    private bool  mInputLocked  = false;
    private float mTargetSideOffset;
    private int   mCollisionMask;

    void Start()
    {
        if (target == null)
            Debug.LogError("카메라의 Target(공)을 지정해주세요!");

        mCollisionMask    = ~LayerMask.GetMask("GolfBall", "Ignore Raycast");
        mTargetSideOffset = 0f;           // 조준 단계: 공 정 뒤에서 시작
        mCameraYaw        = mAimYaw;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ── 입력 처리 (조준 단계에서만 허용) ─────────────────────────────
        if (!mInputLocked)
        {
            mAimYaw   += Input.GetAxis("Horizontal") * yawSpeed   * Time.deltaTime;
            mAimPitch -= Input.GetAxis("Vertical")   * pitchSpeed * Time.deltaTime;
            mAimPitch  = Mathf.Clamp(mAimPitch, minPitch, maxPitch);
        }
        else
        {
            // 파워 단계: pitch를 mChargePitch로 부드럽게 이동
            mAimPitch = Mathf.Lerp(mAimPitch, mChargePitch, Time.deltaTime * mTransitionSpeed);
        }

        // ── 카메라 yaw 보간 (조준=사이드 오프셋 / 파워=정뒤) ─────────────
        float targetCameraYaw = mAimYaw + mTargetSideOffset;
        mCameraYaw = Mathf.LerpAngle(mCameraYaw, targetCameraYaw, Time.deltaTime * mTransitionSpeed);

        // ── 카메라 위치 계산 ──────────────────────────────────────────────
        Quaternion camRot   = Quaternion.Euler(mAimPitch, mCameraYaw, 0);
        Vector3    direction = camRot * Vector3.back;

        float actualDistance = distance;
        if (Physics.SphereCast(target.position, collisionRadius, direction,
                               out RaycastHit hit, distance, mCollisionMask))
        {
            actualDistance = Mathf.Max(hit.distance - collisionRadius, minDistance);
        }

        transform.position = camRot * new Vector3(0f, 0f, -actualDistance) + target.position;
        transform.LookAt(target.position); // 항상 공을 바라봄
    }

    // 파워 단계로 전환: 카메라를 사이드뷰로 이동 (포물선 호가 잘 보이게)
    public void LockForCharging()
    {
        mInputLocked      = true;
        mTargetSideOffset = mAimSideOffset; // 사이드뷰로 이동
    }

    // 조준 단계로 복귀: 카메라를 공 정 뒤로
    public void Unlock()
    {
        mInputLocked      = false;
        mTargetSideOffset = 0f; // 정뒤로 복귀
    }

    // 발사 방향 (조준 yaw 기준)
    public Vector3 GetLookDirection()
    {
        float yawRad = mAimYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
    }

    // 발사각: 수평일수록 높게, 아래 볼수록 낮게
    public float GetPitchAngle()
    {
        return minPitch + maxPitch - mAimPitch;
    }
}
