using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AstronautThirdPersonCamera
{
    public class AstronautThirdPersonCamera : MonoBehaviour
    {
        [Header("Camera Stand (셀카봉 지지대)")]
        // 방금 만든 부모 오브젝트 'CameraStand'를 여기에 드래그앤드롭 할 거야
        [SerializeField] private Transform mCameraStand;    

        [Header("Distance Settings (스케일에 맞게 수치 조정 가능)")]
        [SerializeField] private float mCameraWidth = -5.0f;  // 캐릭터 뒤쪽으로 떨어질 거리 (음수)
        [SerializeField] private float mCameraHeight = 2.0f;  // 캐릭터 위쪽으로 올라갈 높이
        [SerializeField] private float mCameraFixDist = 0.5f; // 벽에 부딪혔을 때 파고듬을 막는 보정치

        [Header("Rotation Limits")]
        [SerializeField] private float mLowestRot = 30.0f;    // 아래를 내려다보는 한계 각도
        [SerializeField] private float mHighestRot = 50.0f;   // 위를 올려다보는 한계 각도

        [Header("Mouse Sensitivity")]
        public float sensitivityX = 2.0f;
        public float sensitivityY = 2.0f;

        private float mCameraDist;
        private Vector3 mDirectionForArmToCam;
        private Vector3 mRayTarget;
        private RaycastHit mHitInfo;

        void Start()
        {
            // 만약 인스펙터에서 스탠드를 지정 안 했다면 자동으로 부모 오브젝트를 찾아 연결
            if (mCameraStand == null && transform.parent != null)
            {
                mCameraStand = transform.parent;
            }

            // 피타고라스 정리를 이용해 실제 대각선 카메라 거리를 계산
            mCameraDist = Mathf.Sqrt(mCameraWidth * mCameraWidth + mCameraHeight * mCameraHeight);

            // 스탠드에서 카메라 위치까지의 방향 벡터를 구함
            mDirectionForArmToCam = new Vector3(0, mCameraHeight, mCameraWidth).normalized;
        }

        void Update()
        {
            if (mCameraStand == null) return;

            LookAround();
            CameraMove();
        }

        // 1. 마우스 입력에 따라 '카메라 스탠드'를 회전시키는 함수
        private void LookAround()
        {
            // 마우스 이동량에 민감도 반영
            Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X") * sensitivityX, Input.GetAxis("Mouse Y") * sensitivityY);
            Vector3 camAngle = mCameraStand.rotation.eulerAngles;

            // 유니티 eulerAngles 특성을 고려한 상하 각도 제한 (Clamping)
            // 상하 조작 반전 문제를 해결하기 위해 mouseDelta.y를 더해줌 (+값으로 정상화)
            float x = camAngle.x - mouseDelta.y; 
            
            if (x < 180f)
            {
                x = Mathf.Clamp(x, -1f, mHighestRot);
            }
            else
            {
                x = Mathf.Clamp(x, 360f - mLowestRot, 360f);
            }

            // 카메라 자체가 아니라 부모인 mCameraStand의 회전값을 변경함
            mCameraStand.rotation = Quaternion.Euler(x, camAngle.y + mouseDelta.x, camAngle.z);
        }

        // 2. 레이케스트 충돌을 감지하고 카메라의 최종 위치를 결정하는 함수
        private void CameraMove()
        {
            // 카메라의 실시간 위치 기준점을 스탠드(캐릭터 위치)로 초기화
            transform.position = mCameraStand.position;

            // 레이캐스트를 쏠 목표 로컬 방향 벡터 계산
            mRayTarget = mCameraStand.up * mCameraHeight + mCameraStand.forward * mCameraWidth;

            // 캐릭터 원점에서 카메라가 가야 할 방향으로 얇고 날카로운 Ray를 쏨 (본인 몸통 간섭 없음!)
            if (Physics.Raycast(mCameraStand.position, mRayTarget, out mHitInfo, mCameraDist))
            {
                // 벽이나 바닥(오브젝트)에 부딪혔다면, 충돌한 지점(point)으로 카메라를 즉시 당김
                transform.position = mHitInfo.point;

                // 벽 속으로 카메라가 살짝 파고드는 현상을 막기 위해 캐릭터 쪽으로 조금 더 앞당김 (보정)
                transform.Translate(mDirectionForArmToCam * -1f * mCameraFixDist);
            }
            else
            {
                // 아무것도 부딪히지 않았다면 원래 가야 할 최대 거리만큼 떨어뜨림
                transform.localPosition = Vector3.zero;
                transform.Translate(mDirectionForArmToCam * mCameraDist);
                transform.Translate(mDirectionForArmToCam * -1f * mCameraFixDist);
            }
        }
    }
}