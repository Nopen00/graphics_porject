using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 바람 방향/세기 UI.
/// GolfHUD 아래에 화살표 Image와 Text를 연결하면 동작.
/// 발사마다 바람을 랜덤으로 바꾸는 기능 포함.
/// </summary>
public class WindUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private RectTransform mArrow;     // 방향 화살표 Image (위쪽이 북쪽)
    [SerializeField] private Text          mSpeedText; // 바람 세기 텍스트

    [Header("바람 설정")]
    [SerializeField] private bool  mRandomizePerShot = true; // 발사마다 바람 랜덤 변경
    [SerializeField] private float mMinWindSpeed     = 0f;
    [SerializeField] private float mMaxWindSpeed     = 6f;

    private GolfBallPhysics mBall;
    private int             mLastStrokeCount = 0;

    void Start()
    {
        mBall = FindFirstObjectByType<GolfBallPhysics>();
        RefreshUI();
    }

    void Update()
    {
        if (mBall == null) return;

        // 스트로크 카운트가 증가하면 발사된 것으로 판단
        if (mBall.StrokeCount != mLastStrokeCount)
        {
            mLastStrokeCount = mBall.StrokeCount;
            if (mRandomizePerShot)
                RandomizeWind();
            else
                RefreshUI();
        }
    }

    private void RandomizeWind()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float speed = Random.Range(mMinWindSpeed, mMaxWindSpeed);
        mBall.WindVector = new Vector3(Mathf.Sin(angle) * speed, 0f, Mathf.Cos(angle) * speed);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (mBall == null) return;

        Vector3 wind  = mBall.WindVector;
        float   speed = new Vector2(wind.x, wind.z).magnitude;

        // 화살표 회전: XZ 벡터를 UI Z축 회전으로 변환
        // atan2(x, z) → 북쪽(0,1)이 위를 향하는 화살표 기준
        float angleDeg = Mathf.Atan2(wind.x, wind.z) * Mathf.Rad2Deg;
        if (mArrow != null)
            mArrow.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);

        if (mSpeedText != null)
            mSpeedText.text = speed < 0.1f ? "바람 없음" : $"바람  {speed:F1} m/s";
    }
}
