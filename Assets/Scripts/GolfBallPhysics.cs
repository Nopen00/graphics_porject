using UnityEngine;

/// <summary>
/// 골프공 물리 시뮬레이션
/// - 발사체 운동 (포물선), 바람, 표면 마찰, 충돌 반발계수를 직접 계산
/// - Rigidbody는 충돌 감지 전용으로만 사용 (useGravity = false)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GolfBallPhysics : MonoBehaviour
{
    public enum BallState  { Idle, InFlight, Rolling }
    public enum AimPhase   { Aiming, Charging }

    public BallState State { get; private set; } = BallState.Idle;
    public AimPhase  Phase { get; private set; } = AimPhase.Aiming;

    // ── 발사 설정 ─────────────────────────────────────────────────────────
    [Header("발사 설정")]
    [SerializeField] private float mMaxPower        = 30f;  // 최대 발사 속력 (m/s)
    [SerializeField] private float mPowerChargeTime = 2.0f; // 최대 파워까지 충전 시간 (초)

    // ── 물리 상수 ─────────────────────────────────────────────────────────
    [Header("물리 상수")]
    [SerializeField] private float mRestitution   = 0.6f;  // 반발계수 e (0=완전비탄성, 1=완전탄성)
    [SerializeField] private float mStopThreshold = 0.25f; // 이 속력 이하면 정지로 판정 (m/s)

    // ── 표면 마찰계수 μ ────────────────────────────────────────────────────
    [Header("표면 마찰계수 (μ) — 태그: Fairway / Rough / Bunker")]
    [SerializeField] private float mFairwayFriction = 0.25f;
    [SerializeField] private float mRoughFriction   = 0.55f;
    [SerializeField] private float mBunkerFriction  = 0.80f;

    // ── 바람 ──────────────────────────────────────────────────────────────
    [Header("바람 벡터 (GameManager에서 설정 가능)")]
    public Vector3 WindVector = new Vector3(2f, 0f, 0f);

    // ── UI ────────────────────────────────────────────────────────────────
    [Header("UI 연결")]
    [SerializeField] private UnityEngine.UI.Slider mPowerSlider; // 파워바 슬라이더
    [SerializeField] private UnityEngine.UI.Text   mStrokeText;  // 스트로크 카운트 텍스트

    // ── 내부 상태 ─────────────────────────────────────────────────────────
    private Rigidbody        mRb;
    private CameraController mGolfCamera;

    private Vector3 mVelocity       = Vector3.zero;
    private float   mCurrentPower   = 0f;
    private bool    mCharging       = false;
    private float   mCurrentFriction;

    public int   StrokeCount  { get; private set; } = 0;
    public float CurrentPower { get; private set; } = 0f; // TrajectoryPredictor에서 읽어감

    // ─────────────────────────────────────────────────────────────────────

    void Start()
    {
        mRb = GetComponent<Rigidbody>();
        mRb.useGravity  = true;    // 대기 중에는 Unity 기본 중력으로 지면에 붙어 있음
        mRb.isKinematic = false;
        mRb.interpolation          = RigidbodyInterpolation.Interpolate;
        mRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        mGolfCamera      = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        mCurrentFriction = mFairwayFriction;

        if (mPowerSlider != null) mPowerSlider.value = 0f;
        UpdateStrokeUI();
    }

    // 입력은 Update, 물리는 FixedUpdate에서 처리
    void Update()
    {
        if (!IsGolfMode()) return;
        if (State != BallState.Idle) return;

        if (Phase == AimPhase.Aiming)
            HandleAiming();
        else
            HandlePowerCharge();
    }

    void FixedUpdate()
    {
        if (!IsGolfMode()) return;

        switch (State)
        {
            case BallState.InFlight: UpdateFlight();  break;
            case BallState.Rolling:  UpdateRolling(); break;
        }
    }

    // ── 조준 단계: 스페이스로 방향 확정 ──────────────────────────────────
    private void HandleAiming()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Phase = AimPhase.Charging;
            if (mGolfCamera != null) mGolfCamera.LockForCharging();
        }
    }

    // ── 파워 충전 입력 ────────────────────────────────────────────────────
    private void HandlePowerCharge()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mCharging     = true;
            mCurrentPower = 0f;
        }

        if (mCharging && Input.GetMouseButton(0))
        {
            mCurrentPower += (mMaxPower / mPowerChargeTime) * Time.deltaTime;
            mCurrentPower  = Mathf.Min(mCurrentPower, mMaxPower);
            CurrentPower   = mCurrentPower; // 외부 읽기용 동기화

            if (mPowerSlider != null)
                mPowerSlider.value = mCurrentPower / mMaxPower;
        }

        if (mCharging && Input.GetMouseButtonUp(0))
            Launch();
    }

    // ── 발사 ──────────────────────────────────────────────────────────────
    private void Launch()
    {
        mCharging = false;
        if (mPowerSlider != null) mPowerSlider.value = 0f;
        if (mGolfCamera == null) { Debug.LogWarning("GolfCamera 없음"); return; }

        StrokeCount++;
        UpdateStrokeUI();

        // 수평 방향: 카메라 aim 방향 (XZ 평면)
        Vector3 horizontalDir = mGolfCamera.GetLookDirection();

        // 발사각: 카메라 피치 각도 (도 → 라디안)
        float pitchRad = mGolfCamera.GetPitchAngle() * Mathf.Deg2Rad;

        // ── 포물선 운동 초기 속도 분해 ──────────────────────────────────
        // v_xz = v₀ · cos(θ)   (수평 성분)
        // v_y  = v₀ · sin(θ)   (수직 성분)
        float hSpeed = mCurrentPower * Mathf.Cos(pitchRad);
        float vSpeed = mCurrentPower * Mathf.Sin(pitchRad);

        mRb.useGravity   = false;
        mRb.constraints  = RigidbodyConstraints.None;
        mVelocity          = horizontalDir * hSpeed + Vector3.up * vSpeed;
        mRb.linearVelocity = mVelocity;
        State              = BallState.InFlight;
        if (mGolfCamera != null) mGolfCamera.StartTracking();
    }

    // ── 비행 물리 (FixedUpdate) ───────────────────────────────────────────
    private void UpdateFlight()
    {
        // CustomPhysicsEngine: v = v₀ + (g + windCoeff · wind) · Δt
        mVelocity    = CustomPhysicsEngine.CalculateNextVelocity(mVelocity, WindVector, Time.fixedDeltaTime);
        mRb.linearVelocity = mVelocity;
    }

    // ── 구르기 물리 (FixedUpdate) ─────────────────────────────────────────
    private void UpdateRolling()
    {
        float speed = mVelocity.magnitude;

        if (speed < mStopThreshold)
        {
            StopBall();
            return;
        }

        // 마찰 감속: a = μg,  v = v₀ - μg · Δt
        float decel = mCurrentFriction * Mathf.Abs(Physics.gravity.y);
        mVelocity -= mVelocity.normalized * decel * Time.fixedDeltaTime;
        mVelocity.y  = 0f;          // 지면 위에서는 Y 고정
        mRb.linearVelocity = mVelocity;
    }

    // ── 충돌 감지 & 반발계수 적용 ─────────────────────────────────────────
    void OnCollisionEnter(Collision col)
    {
        if (State != BallState.InFlight) return;

        Vector3 normal = col.GetContact(0).normal;
        float   vDotN  = Vector3.Dot(mVelocity, normal);

        // 이미 표면에서 멀어지는 방향이면 무시
        if (vDotN >= 0f) return;

        // ── 반발 공식 ────────────────────────────────────────────────────
        // v' = v - (1 + e)(v · n̂)n̂
        mVelocity          -= (1f + mRestitution) * vDotN * normal;
        mRb.linearVelocity = mVelocity;

        // 바닥 충돌 판정: normal.y > 0.7 이면 위를 향하는 표면 (바닥/경사면)
        // 벽 충돌(normal.y ≈ 0)은 Rolling 전환 안 함 → 공중 고정 버그 방지
        bool isFloor = normal.y > 0.7f;
        if (isFloor && Mathf.Abs(mVelocity.y) < 1.5f)
        {
            mVelocity.y        = 0f;
            mRb.linearVelocity = mVelocity;
            State              = BallState.Rolling;
        }

        UpdateFriction(col.gameObject);
    }

    void OnCollisionStay(Collision col)
    {
        if (State == BallState.Rolling)
            UpdateFriction(col.gameObject);
    }

    // ── 표면 마찰계수 결정: Terrain이면 텍스처 레이어, 아니면 태그 기반 ────
    private void UpdateFriction(GameObject hit)
    {
        if (hit.TryGetComponent<Terrain>(out _))
        {
            mCurrentFriction = GetTerrainFriction(transform.position);
        }
        else
        {
            mCurrentFriction = hit.tag switch
            {
                "Fairway" => mFairwayFriction,
                "Rough"   => mRoughFriction,
                "Bunker"  => mBunkerFriction,
                _         => mFairwayFriction,
            };
        }
    }

    // ── Terrain 위치의 텍스처 레이어로 마찰계수 결정 ─────────────────────
    // TerrainLayer 순서: 0=Fairway, 1=Rough, 2=Bunker
    private float GetTerrainFriction(Vector3 worldPos)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return mFairwayFriction;

        TerrainData data     = terrain.terrainData;
        Vector3     localPos = worldPos - terrain.transform.position;

        // 월드 좌표 → 알파맵 정규화 좌표 (0~1)
        float normX = Mathf.Clamp01(localPos.x / data.size.x);
        float normZ = Mathf.Clamp01(localPos.z / data.size.z);

        int mapX = Mathf.Clamp(Mathf.FloorToInt(normX * data.alphamapWidth),  0, data.alphamapWidth  - 1);
        int mapZ = Mathf.Clamp(Mathf.FloorToInt(normZ * data.alphamapHeight), 0, data.alphamapHeight - 1);

        float[,,] maps = data.GetAlphamaps(mapX, mapZ, 1, 1);

        // 가장 비중이 높은 레이어를 지배 표면으로 결정
        int   dominant = 0;
        float maxVal   = 0f;
        for (int i = 0; i < maps.GetLength(2); i++)
        {
            if (maps[0, 0, i] > maxVal) { maxVal = maps[0, 0, i]; dominant = i; }
        }

        return dominant switch
        {
            0 => mFairwayFriction,
            1 => mRoughFriction,
            2 => mBunkerFriction,
            _ => mFairwayFriction,
        };
    }

    // HoleDetector에서 홀 완료 시 호출 — 공을 홀컵 위치에 고정
    public void ForceStop(Vector3 position)
    {
        transform.position = position;
        StopBall();
    }

    private void StopBall()
    {
        mVelocity           = Vector3.zero;
        mRb.linearVelocity  = Vector3.zero;
        mRb.angularVelocity = Vector3.zero;
        mRb.useGravity      = true;
        mRb.constraints     = RigidbodyConstraints.FreezeAll;
        State               = BallState.Idle;
        Phase               = AimPhase.Aiming;                // 조준 단계로 복귀
        if (mGolfCamera != null) mGolfCamera.Unlock();        // 카메라 사이드뷰로 복귀
    }

    private void UpdateStrokeUI()
    {
        if (mStrokeText != null)
            mStrokeText.text = $"스트로크: {StrokeCount}";
    }

    private bool IsGolfMode() =>
        GameManager.Instance != null &&
        GameManager.Instance.CurrentMode == GameManager.GameMode.Golf;
}
