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
    public enum AimPhase   { Aiming, Charging, Impact }

    public BallState State { get; private set; } = BallState.Idle;
    public AimPhase  Phase { get; private set; } = AimPhase.Aiming;

    // ── 발사 설정 ─────────────────────────────────────────────────────────
    [Header("발사 설정")]
    [SerializeField] private float mMaxPower        = 30f;  // 최대 발사 속력 (m/s)
    [SerializeField] private float mOscillateSpeed  = 1.0f; // 파워바 왕복 속도 (1 = 1초에 0→Max→0)

    // ── 임팩트 설정 ───────────────────────────────────────────────────────
    [Header("임팩트 설정")]
    [SerializeField] private float mMaxImpactAngle = 15f; // 최대 방향 틀어짐 (도)

    // ── 물리 상수 ─────────────────────────────────────────────────────────
    [Header("물리 상수")]
    [SerializeField] private float mRestitution   = 0.6f;  // 반발계수 e (0=완전비탄성, 1=완전탄성)
    [SerializeField] private float mStopThreshold    = 0.25f; // 이 속력 이하면 정지로 판정 (m/s)
    [SerializeField] private float mStopMoveLimit    = 1.0f;  // 정지 대기 중 이 거리 이상 움직이면 타이머 리셋 (m)

    // ── 표면 마찰계수 μ ────────────────────────────────────────────────────
    [Header("표면 마찰계수 (μ) — 태그: Fairway / Rough / Bunker")]
    [SerializeField] private float mFairwayFriction = 0.50f;
    [SerializeField] private float mRoughFriction   = 1.10f;
    [SerializeField] private float mBunkerFriction  = 1.60f;

    [Header("착지 굴림 억제 (0=즉시 멈춤, 1=감속 없음)")]
    [SerializeField] private float mLandingRollDamping = 0.5f;

    // ── 바람 ──────────────────────────────────────────────────────────────
    [Header("바람 벡터 (GameManager에서 설정 가능)")]
    public Vector3 WindVector = new Vector3(2f, 0f, 0f);

    // ── UI ────────────────────────────────────────────────────────────────
    [Header("UI 연결")]
    [SerializeField] private UnityEngine.UI.Slider mPowerSlider;  // 파워바 슬라이더
    [SerializeField] private UnityEngine.UI.Slider mImpactSlider; // 임팩트 게이지 슬라이더
    [SerializeField] private GameObject            mPowerHandle;  // PowerSlider  > Handle Slide Area > Handle
    [SerializeField] private GameObject            mImpactHandle; // ImpactSlider > Handle Slide Area > Handle
    [SerializeField] private UnityEngine.UI.Text   mStrokeText;   // 스트로크 카운트 텍스트

    // ── 내부 상태 ─────────────────────────────────────────────────────────
    private Rigidbody        mRb;
    private CameraController mGolfCamera;

    private Vector3 mVelocity        = Vector3.zero;
    private float   mCurrentPower   = 0f;
    private float   mOscillateTime  = 0f;   // 파워바 왕복 타이머
    private float   mImpactGauge    = 0f;   // 임팩트 게이지 현재값 (0~100)
    private float   mImpactAngle    = 0f;   // 발사 방향 오프셋 (도)
    private float   mCurrentFriction;
    private float   mStopTimer      = 0f;  // 정지 대기 타이머
    private Vector3 mStopCheckPos;         // 타이머 시작 시점 위치
    private Vector3 mLastShotPosition;     // OB 복귀용 발사 직전 위치

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

        if (mPowerSlider  != null) mPowerSlider.value = 0f;
        if (mPowerHandle  != null) mPowerHandle.SetActive(true);
        if (mImpactHandle != null) mImpactHandle.SetActive(false);
        UpdateStrokeUI();
    }

    // 입력은 Update, 물리는 FixedUpdate에서 처리
    void Update()
    {
        if (!IsGolfMode()) return;
        if (State != BallState.Idle) return;

        switch (Phase)
        {
            case AimPhase.Aiming:   HandleAiming();      break;
            case AimPhase.Charging: HandlePowerCharge(); break;
            case AimPhase.Impact:   HandleImpact();      break;
        }
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

    // ── 파워 자동 왕복 + 스페이스로 파워 확정 ───────────────────────────
    private void HandlePowerCharge()
    {
        mOscillateTime += Time.deltaTime * mOscillateSpeed;
        mCurrentPower   = Mathf.PingPong(mOscillateTime, 1f) * mMaxPower;
        CurrentPower    = mCurrentPower;

        if (mPowerSlider != null)
            mPowerSlider.value = mCurrentPower / mMaxPower;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 파워 확정 → 임팩트 단계로 전환 (파워 슬라이더는 각도 결정 전까지 고정)
            float powerPct  = mCurrentPower / mMaxPower * 100f; // 0~100
            mImpactGauge    = powerPct;
            mOscillateTime  = 0f;
            if (mPowerHandle  != null) mPowerHandle.SetActive(false);
            if (mImpactHandle != null) mImpactHandle.SetActive(true);
            Phase = AimPhase.Impact;
        }
    }

    // ── 임팩트 게이지 하강 + 스페이스로 임팩트 확정 ─────────────────────
    private void HandleImpact()
    {
        // 파워 게이지와 동일한 속도로 하강 (100 → 0)
        mImpactGauge -= Time.deltaTime * mOscillateSpeed * 100f;
        mImpactGauge  = Mathf.Max(0f, mImpactGauge);

        if (mImpactSlider != null)
            mImpactSlider.value = mImpactGauge / 100f;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 게이지 절대값이 곧 zone — 9 초과면 유효 윈도우 전 → 헛스윙
            if (mImpactGauge > 9f)
            {
                Debug.Log($"[임팩트] 헛스윙 — 너무 일찍 (게이지 {mImpactGauge:F1})");
                MissSwing();
                return;
            }

            mImpactAngle = CalculateImpactAngle(mImpactGauge);
            Debug.Log($"[임팩트] zone {mImpactGauge:F1} → 각도 오프셋 {mImpactAngle:F1}°");
            Launch();
        }

        // 게이지 0 도달 → 헛스윙
        if (mImpactGauge <= 0f)
        {
            Debug.Log("[임팩트] 헛스윙 — 너무 늦게");
            MissSwing();
        }
    }

    // ── 임팩트 존 → 각도 오프셋 계산 ─────────────────────────────────────
    // zone 0~3: 오른쪽(+), 4~6: 중앙(0), 6~9: 왼쪽(-)
    private float CalculateImpactAngle(float zone)
    {
        if (zone < 4f)
            return Mathf.Lerp(mMaxImpactAngle, 0f, zone / 4f);   // 0→+15, 4→0
        else if (zone <= 6f)
            return 0f;
        else
            return Mathf.Lerp(0f, -mMaxImpactAngle, (zone - 6f) / 3f); // 6→0, 9→-15
    }

    // ── OB: 발사 위치로 복귀 + 벌타 1타 ─────────────────────────────────
    public void OutOfBounds()
    {
        mVelocity          = Vector3.zero;
        mRb.linearVelocity = Vector3.zero;
        mRb.useGravity     = true;
        mRb.constraints    = RigidbodyConstraints.FreezeRotation;
        transform.position = mLastShotPosition;

        mOscillateTime = 0f;
        mImpactGauge   = 0f;
        mImpactAngle   = 0f;
        mStopTimer     = 0f;
        if (mPowerSlider  != null) mPowerSlider.value  = 0f;
        if (mImpactSlider != null) mImpactSlider.value = 0f;
        if (mPowerHandle  != null) mPowerHandle.SetActive(true);
        if (mImpactHandle != null) mImpactHandle.SetActive(false);

        StrokeCount++;   // 벌타 1타
        UpdateStrokeUI();

        State = BallState.Idle;
        Phase = AimPhase.Aiming;
        if (mGolfCamera != null) mGolfCamera.Unlock();
        Debug.Log($"[OB] 장외 — 벌타 적용, 발사 위치로 복귀 (총 {StrokeCount}타)");
    }

    // ── 헛스윙: 조준 단계로 복귀 ─────────────────────────────────────────
    private void MissSwing()
    {
        mOscillateTime = 0f;
        mImpactGauge   = 0f;
        mImpactAngle   = 0f;
        if (mPowerSlider  != null) mPowerSlider.value  = 0f;
        if (mImpactSlider != null) mImpactSlider.value = 0f;
        if (mPowerHandle  != null) mPowerHandle.SetActive(true);
        if (mImpactHandle != null) mImpactHandle.SetActive(false);
        Phase = AimPhase.Aiming;
        if (mGolfCamera != null) mGolfCamera.Unlock();
    }

    // ── 발사 ──────────────────────────────────────────────────────────────
    private void Launch()
    {
        mOscillateTime = 0f;
        mImpactAngle   = 0f;
        if (mPowerSlider  != null) mPowerSlider.value  = 0f;
        if (mImpactSlider != null) mImpactSlider.value = 0f;
        if (mPowerHandle  != null) mPowerHandle.SetActive(true);
        if (mImpactHandle != null) mImpactHandle.SetActive(false);
        if (mGolfCamera == null) { Debug.LogWarning("GolfCamera 없음"); return; }

        mLastShotPosition = transform.position;
        StrokeCount++;
        UpdateStrokeUI();

        // 수평 방향: 카메라 aim 방향 + 임팩트 각도 오프셋 적용
        // horizontalDir' = Rotate(horizontalDir, mImpactAngle, Y축)
        Vector3 horizontalDir = Quaternion.Euler(0f, mImpactAngle, 0f) * mGolfCamera.GetLookDirection();

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
        float xzSpeed = new Vector2(mVelocity.x, mVelocity.z).magnitude;

        if (xzSpeed < mStopThreshold)
        {
            // 1차 신호: 속도 임계값 이하 → 타이머 시작
            if (mStopTimer == 0f)
                mStopCheckPos = transform.position;

            mStopTimer += Time.fixedDeltaTime;

            // 3초 동안 큰 움직임 없으면 완전 정지
            float moved = Vector3.Distance(transform.position, mStopCheckPos);
            if (mStopTimer >= 3f && moved < mStopMoveLimit)
            {
                StopBall();
                return;
            }
        }
        else
        {
            // 속도가 다시 올라오면 타이머 리셋 (언덕 굴러내리는 중)
            mStopTimer = 0f;
        }

        // ── 경사 가속도 계산 ──────────────────────────────────────────────
        // 레이캐스트로 지면 법선 취득 → 중력을 법선 방향과 평행 방향으로 분해
        // g_slope = g - (g · n̂)n̂  →  경사면을 따라 작용하는 중력 성분
        Vector3 slopeAccelXZ = Vector3.zero;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 1.5f))
        {
            Vector3 n       = slopeHit.normal;
            Vector3 gSlope  = Physics.gravity - Vector3.Dot(Physics.gravity, n) * n;
            slopeAccelXZ    = new Vector3(gSlope.x, 0f, gSlope.z);
        }

        // 마찰 감속 + 경사 중력 가속도 합산
        float   decel      = mCurrentFriction * Mathf.Abs(Physics.gravity.y);
        Vector3 xzVelocity = new Vector3(mVelocity.x, 0f, mVelocity.z);
        float   xzSpeedRoll = xzVelocity.magnitude;
        float   decelDelta  = decel * Time.fixedDeltaTime;
        xzVelocity  = xzSpeedRoll > decelDelta ? xzVelocity.normalized * (xzSpeedRoll - decelDelta) : Vector3.zero;
        xzVelocity += slopeAccelXZ * Time.fixedDeltaTime;

        mVelocity.x        = xzVelocity.x;
        mVelocity.z        = xzVelocity.z;
        mVelocity.y        = mRb.linearVelocity.y;
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
            mVelocity.x        *= mLandingRollDamping;
            mVelocity.z        *= mLandingRollDamping;
            mVelocity.y        = 0f;
            mRb.linearVelocity = mVelocity;
            mRb.useGravity     = true; // 경사면 굴러내리기 위해 중력 복원
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
        mStopTimer          = 0f;
        mImpactGauge        = 0f;
        mImpactAngle        = 0f;
        if (mImpactSlider != null) mImpactSlider.value = 0f;
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
