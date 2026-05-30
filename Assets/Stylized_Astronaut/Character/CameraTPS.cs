using UnityEngine;

public class CameraTPS : MonoBehaviour
{
    [Header("타겟 설정")]
    [SerializeField] private Transform mTarget;           // 캐릭터의 머리 Transform 연결
    [SerializeField] private float mHeightOffset = 0.1f; // 머리에서 추가 높이 오프셋

    [Header("카메라 거리 설정")]
    [SerializeField] private float mDefaultDistance = 3.5f; // 기본 카메라 거리
    [SerializeField] private float mMinDistance = 0.3f;     // 최소 카메라 거리 (벽 붙었을 때)
    [SerializeField] private float mCollisionRadius = 0.2f; // 충돌 감지 구(Sphere) 반경

    [Header("회전 각도 제한")]
    [SerializeField] private float mMinVerticalAngle = -15.0f; // 아래쪽 시야각 한계
    [SerializeField] private float mMaxVerticalAngle = 70.0f;  // 위쪽 시야각 한계

    [Header("마우스 민감도")]
    [SerializeField] private float mSensitivityX = 3.0f;
    [SerializeField] private float mSensitivityY = 2.0f;

    [Header("카메라 보간 설정")]
    [SerializeField] private float mPositionSmooth = 12.0f; // 위치 보간 속도 (높을수록 즉각 반응)

    private float mCurrentYaw;
    private float mCurrentPitch = 15f;
    private int mCollisionMask;

    void Start()
    {
        mCurrentYaw = transform.eulerAngles.y;

        // Player, Ignore Raycast 레이어는 충돌 대상에서 제외
        mCollisionMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
    }

    // LateUpdate: 캐릭터 이동이 끝난 뒤 카메라 처리
    void LateUpdate()
    {
        if (mTarget == null) return;

        RotateCamera();
        PositionCamera();
    }

    private void RotateCamera()
    {
        mCurrentYaw   += Input.GetAxis("Mouse X") * mSensitivityX;
        mCurrentPitch -= Input.GetAxis("Mouse Y") * mSensitivityY; // 마우스 위 → 카메라 위
        mCurrentPitch  = Mathf.Clamp(mCurrentPitch, mMinVerticalAngle, mMaxVerticalAngle);
    }

    private void PositionCamera()
    {
        // 피벗: 머리 위치 + 높이 오프셋
        Vector3 pivot = mTarget.position + Vector3.up * mHeightOffset;

        // 현재 각도에 따른 카메라 방향 (피벗 기준 뒤쪽)
        Quaternion rotation  = Quaternion.Euler(mCurrentPitch, mCurrentYaw, 0f);
        Vector3    direction = rotation * Vector3.back;

        // SphereCast: 피벗 → 카메라 방향으로 구를 굴려 장애물 감지
        float targetDistance = mDefaultDistance;
        if (Physics.SphereCast(pivot, mCollisionRadius, direction, out RaycastHit hit, mDefaultDistance, mCollisionMask))
        {
            // 충돌 지점까지의 거리에서 반경만큼 빼서 카메라가 벽 안으로 들어가지 않게 함
            targetDistance = Mathf.Max(hit.distance - mCollisionRadius, mMinDistance);
        }

        Vector3 targetPosition = pivot + direction * targetDistance;

        // 부드럽게 이동 후 피벗을 바라봄
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * mPositionSmooth);
        transform.LookAt(pivot);
    }
}
