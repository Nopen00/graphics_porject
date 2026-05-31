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
    [SerializeField] private float mAimSideOffset   = 35f;
    [SerializeField] private float mChargePitch      = 15f;
    [SerializeField] private float mTransitionSpeed  = 5f;

    [Header("트래킹 카메라 설정")]
    [SerializeField] private float mTrackPitch      = 20f;  // 트래킹 중 수직 각도
    [SerializeField] private float mTrackDistance   = 12f;  // 트래킹 중 거리
    [SerializeField] private float mTrackLerpSpeed  = 4f;   // 위치 보간 속도

    private enum CameraState { Aiming, Charging, Tracking }
    private CameraState mState = CameraState.Aiming;

    private float mAimYaw   = 0f;
    private float mAimPitch = 40f;
    private float mCameraYaw;
    private float mTargetSideOffset;
    private int   mCollisionMask;

    void Start()
    {
        if (target == null)
            Debug.LogError("카메라의 Target(공)을 지정해주세요!");

        mCollisionMask    = ~LayerMask.GetMask("GolfBall", "Ignore Raycast");
        mTargetSideOffset = 0f;
        mCameraYaw        = mAimYaw;
    }

    void LateUpdate()
    {
        if (target == null) return;

        switch (mState)
        {
            case CameraState.Aiming:    UpdateAiming();   break;
            case CameraState.Charging:  UpdateCharging(); break;
            case CameraState.Tracking:  UpdateTracking(); break;
        }
    }

    // ── 조준 단계: 입력으로 회전 ─────────────────────────────────────────
    private void UpdateAiming()
    {
        mAimYaw   += Input.GetAxis("Horizontal") * yawSpeed   * Time.deltaTime;
        mAimPitch -= Input.GetAxis("Vertical")   * pitchSpeed * Time.deltaTime;
        mAimPitch  = Mathf.Clamp(mAimPitch, minPitch, maxPitch);

        float targetCameraYaw = mAimYaw + mTargetSideOffset;
        mCameraYaw = Mathf.LerpAngle(mCameraYaw, targetCameraYaw, Time.deltaTime * mTransitionSpeed);

        ApplyOrbitalPosition(mAimPitch, mCameraYaw, distance);
    }

    // ── 파워 단계: pitch를 사이드뷰로 보간 ──────────────────────────────
    private void UpdateCharging()
    {
        mAimPitch = Mathf.Lerp(mAimPitch, mChargePitch, Time.deltaTime * mTransitionSpeed);

        float targetCameraYaw = mAimYaw + mTargetSideOffset;
        mCameraYaw = Mathf.LerpAngle(mCameraYaw, targetCameraYaw, Time.deltaTime * mTransitionSpeed);

        ApplyOrbitalPosition(mAimPitch, mCameraYaw, distance);
    }

    // ── 트래킹 단계: 공 뒤를 Lerp로 따라감 ──────────────────────────────
    private void UpdateTracking()
    {
        // 발사 방향(mAimYaw) 그대로 유지 — 공이 카메라에서 멀어지는 방향으로 날아감
        mCameraYaw = Mathf.LerpAngle(mCameraYaw, mAimYaw, Time.deltaTime * mTrackLerpSpeed);
        mAimPitch  = Mathf.Lerp(mAimPitch, mTrackPitch, Time.deltaTime * mTrackLerpSpeed);

        // 벽 충돌 없이 Lerp로 위치 이동
        Quaternion camRot     = Quaternion.Euler(mAimPitch, mCameraYaw, 0);
        Vector3    desiredPos = camRot * new Vector3(0f, 0f, -mTrackDistance) + target.position;

        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * mTrackLerpSpeed);
        transform.LookAt(target.position);
    }

    // ── 공통: 구면 좌표계로 카메라 위치 계산 (벽 충돌 포함) ─────────────
    private void ApplyOrbitalPosition(float pitch, float yaw, float dist)
    {
        Quaternion camRot  = Quaternion.Euler(pitch, yaw, 0);
        Vector3    dir     = camRot * Vector3.back;

        float actualDist = dist;
        if (Physics.SphereCast(target.position, collisionRadius, dir,
                               out RaycastHit hit, dist, mCollisionMask))
        {
            actualDist = Mathf.Max(hit.distance - collisionRadius, minDistance);
        }

        transform.position = camRot * new Vector3(0f, 0f, -actualDist) + target.position;
        transform.LookAt(target.position);
    }

    // ── 외부 호출 ────────────────────────────────────────────────────────

    // 발사 시 GolfBallPhysics에서 호출
    public void StartTracking()
    {
        mState            = CameraState.Tracking;
        mTargetSideOffset = 0f;
    }

    // 파워 충전 시 호출
    public void LockForCharging()
    {
        mState            = CameraState.Charging;
        mTargetSideOffset = mAimSideOffset;
    }

    // 공 정지 시 호출 — 조준으로 복귀
    public void Unlock()
    {
        mState            = CameraState.Aiming;
        mTargetSideOffset = 0f;
    }

    public Vector3 GetLookDirection()
    {
        float yawRad = mAimYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
    }

    public float GetPitchAngle()
    {
        return minPitch + maxPitch - mAimPitch;
    }
}
