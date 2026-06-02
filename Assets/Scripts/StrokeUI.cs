using UnityEngine;
using UnityEngine.UI;

public class StrokeUI : MonoBehaviour
{
    [SerializeField] private Text mText;

    private GolfBallPhysics mBall;
    private int mLastStroke = -1;

    void Start()
    {
        mBall = FindFirstObjectByType<GolfBallPhysics>();
        Refresh();
    }

    void Update()
    {
        if (mBall == null) return;
        if (mBall.StrokeCount != mLastStroke)
            Refresh();
    }

    private void Refresh()
    {
        if (mBall == null || mText == null) return;
        mLastStroke = mBall.StrokeCount;
        mText.text = $"스트로크: {mLastStroke}";
    }
}
