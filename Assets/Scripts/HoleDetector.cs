using UnityEngine;

/// <summary>
/// 홀컵 GameObject에 붙이는 스크립트.
/// IsTrigger 켜진 Collider 필요. GolfBallPhysics 컴포넌트로 공을 식별하므로 태그 불필요.
/// ResultPanel/ResultText 미연결 시 OnGUI로 화면 중앙에 결과를 표시함.
/// </summary>
public class HoleDetector : MonoBehaviour
{
    [Header("홀 진입 설정")]
    [SerializeField] private float mMaxEntrySpeed = 3f; // 이 속력 이하일 때만 홀 인정 (m/s)

    [Header("결과 UI (미연결 시 OnGUI로 자동 표시)")]
    [SerializeField] private GameObject      mResultPanel;
    [SerializeField] private UnityEngine.UI.Text mResultText;

    private GolfBallPhysics mBall;
    private bool            mCompleted  = false;
    private string          mResultMsg  = "";

    void Start()
    {
        mBall = FindFirstObjectByType<GolfBallPhysics>();
        if (mResultPanel != null) mResultPanel.SetActive(false);

        if (mBall == null)
            Debug.LogError("[HoleDetector] GolfBallPhysics를 찾을 수 없습니다.");
    }

    void OnTriggerEnter(Collider other)
    {
        if (mCompleted) return;

        GolfBallPhysics ball = other.GetComponent<GolfBallPhysics>();
        if (ball == null) return;

        // 속도 체크: mMaxEntrySpeed 초과 시 홀 통과 (실제 골프처럼)
        Rigidbody rb = other.attachedRigidbody;
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (speed > mMaxEntrySpeed)
        {
            Debug.Log($"[HoleDetector] 속도 {speed:F1} m/s — 너무 빨라서 통과");
            return;
        }

        mCompleted = true;
        Debug.Log($"[HoleDetector] 홀 완료! {ball.StrokeCount} 타 (진입 속도 {speed:F1} m/s)");
        BallInHole(ball);
    }

    private void BallInHole(GolfBallPhysics ball)
    {
        ball.ForceStop(transform.position);

        mResultMsg = $"{ball.StrokeCount} 타!";

        if (mResultPanel != null)
        {
            mResultPanel.SetActive(true);
            if (mResultText != null)
                mResultText.text = mResultMsg;
        }
    }

    // ResultPanel 미연결 시 폴백 — 항상 화면에 표시
    void OnGUI()
    {
        if (!mCompleted) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 60,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = Color.yellow;

        float w = 500f, h = 200f;
        Rect rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.Label(rect, $"홀 완료!\n{mResultMsg}", style);
    }
}
