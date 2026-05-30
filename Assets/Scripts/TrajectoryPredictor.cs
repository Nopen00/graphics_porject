using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발사 전 궤적 예측선 (LineRenderer)
/// GolfBallPhysics와 동일한 물리 공식으로 미래 위치를 미리 계산해 표시
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TrajectoryPredictor : MonoBehaviour
{
    [Header("시뮬레이션 설정")]
    [SerializeField] private int   mMaxSteps     = 80;    // 최대 예측 스텝 수
    [SerializeField] private float mGravityScale = 1.0f;  // 예측선 중력 배율 (1=기본 9.81, 높을수록 더 빨리 떨어짐)
    [SerializeField] private float mPreviewPower = 15f;   // 파워 0일 때 보여줄 기본 미리보기 파워

    [Header("점선 효과")]
    [SerializeField] private int   mDotGap    = 2;     // 이 스텝마다 선을 끊음 (클수록 듬성듬성)

    [Header("색상")]
    [SerializeField] private Color mStartColor = Color.yellow;
    [SerializeField] private Color mEndColor   = new Color(1f, 1f, 0f, 0f); // 끝갈수록 투명

    private LineRenderer     mLine;
    private GolfBallPhysics  mBallPhysics;
    private CameraController mGolfCamera;

    void Start()
    {
        mLine        = GetComponent<LineRenderer>();
        mBallPhysics = GetComponent<GolfBallPhysics>();
        mGolfCamera  = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);

        mLine.startColor  = mStartColor;
        mLine.endColor    = mEndColor;
        mLine.useWorldSpace = true;
        mLine.enabled     = false;
    }

    void Update()
    {
        bool shouldShow = GameManager.Instance != null
                       && GameManager.Instance.CurrentMode == GameManager.GameMode.Golf
                       && mBallPhysics.State == GolfBallPhysics.BallState.Idle;

        if (!shouldShow) { mLine.enabled = false; return; }

        mLine.enabled = true;
        DrawTrajectory();
    }

    private void DrawTrajectory()
    {
        if (mGolfCamera == null) return;

        // ── 초기 속도 계산 (GolfBallPhysics.Launch()와 완전히 동일한 공식) ──
        Vector3 horizontalDir = mGolfCamera.GetLookDirection();
        float   pitchRad      = mGolfCamera.GetPitchAngle() * Mathf.Deg2Rad;
        // 충전 중이면 실제 파워, 아직 안 눌렀으면 기본 미리보기 파워 사용
        float   power         = mBallPhysics.CurrentPower > 0f
                                ? mBallPhysics.CurrentPower
                                : mPreviewPower;

        // v_xz = v₀·cos(θ),  v_y = v₀·sin(θ)
        Vector3 vel = horizontalDir * (power * Mathf.Cos(pitchRad))
                    + Vector3.up    * (power * Mathf.Sin(pitchRad));

        // ── 미래 위치 시뮬레이션 ────────────────────────────────────────────
        float         dt     = 0.05f;
        Vector3       pos    = transform.position;
        float         floorY = pos.y - 0.1f;
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < mMaxSteps; i++)
        {
            vel = CustomPhysicsEngine.CalculateNextVelocity(vel, mBallPhysics.WindVector, dt);
            vel.y -= (mGravityScale - 1f) * 9.81f * dt; // 기본 중력에 추가 보정
            pos = CustomPhysicsEngine.CalculateNextPosition(pos, vel, dt);

            // 지면 아래로 내려가면 예측 중단
            if (pos.y < floorY) break;

            // ── 점선 효과 ──────────────────────────────────────────────────
            // mDotGap 스텝마다 2개의 점을 같은 위치에 추가해 선을 끊음
            if (i % (mDotGap + 1) == 0)
            {
                if (points.Count > 0)
                {
                    // 직전 점 위치를 한 번 더 추가 → LineRenderer가 길이 0 구간을 만들어 gap 생성
                    points.Add(points[points.Count - 1]);
                }
                points.Add(pos);
            }
            else
            {
                points.Add(pos);
            }
        }

        mLine.positionCount = points.Count;
        if (points.Count > 0)
            mLine.SetPositions(points.ToArray());
    }
}
