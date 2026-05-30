using UnityEngine;
using System.Collections;

namespace AstronautPlayer
{
    public class AstronautPlayer : MonoBehaviour {

        private Animator anim;
        private CharacterController controller;

        [Header("Movement Settings")]
        public float speed = 600.0f;
        public float turnSpeed = 400.0f;
        private Vector3 moveDirection = Vector3.zero;
        public float gravity = 20.0f;

        [Header("Jump Settings")]
        public float jumpSpeed = 8.0f; // <--- [추가] 점프 높이/힘을 제어하는 변수

        void Start () {
            controller = GetComponent <CharacterController>();
            anim = gameObject.GetComponentInChildren<Animator>();
        }

        void Update (){
            // 1. 애니메이션 제어 (W 누르면 걷기)
            if (Input.GetKey ("w")) {
                anim.SetInteger ("AnimationPar", 1);
            }  else {
                anim.SetInteger ("AnimationPar", 0);
            }

            // 2. 수평 이동 (X/Z만 업데이트 - Y는 건드리지 않음)
            Vector3 horizontalMove = transform.forward * (Input.GetAxis("Vertical") * speed);
            moveDirection.x = horizontalMove.x;
            moveDirection.z = horizontalMove.z;

            // 3. 착지 시 낙하 속도 초기화
            // moveDirection.y < 0 조건: 점프 직후 위로 올라가는 중이면 건드리지 않음
            if (controller.isGrounded && moveDirection.y < 0)
            {
                moveDirection.y = -2f; // 0이 아닌 작은 음수 → isGrounded를 안정적으로 유지
            }

            // 4. 점프 (바닥에 있을 때만)
            if (Input.GetButtonDown("Jump") && controller.isGrounded)
            {
                moveDirection.y = jumpSpeed;
            }

            // 5. 중력 누적 (항상 적용)
            moveDirection.y -= gravity * Time.deltaTime;

            // 6. 회전 적용 (A, D 누르면 몸통 회전)
            float turn = Input.GetAxis("Horizontal");
            transform.Rotate(0, turn * turnSpeed * Time.deltaTime, 0);

            // 7. 최종 이동 반영
            controller.Move(moveDirection * Time.deltaTime);
        }
    }
}