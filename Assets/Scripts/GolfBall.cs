using UnityEngine;
using UnityEngine.UI;

public class GolfBall : MonoBehaviour
{
    [Header("상호작용 설정")]
    [SerializeField] private float mInteractRadius = 2.5f; // F키 활성화 반경

    [Header("UI")]
    [SerializeField] private GameObject mInteractPromptUI; // "F: 골프 시작" 텍스트 오브젝트

    private bool   mPlayerInRange    = false;
    private Transform mPlayerTransform = null;

    void Start()
    {
        // "Player" 태그로 캐릭터 자동 탐색
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            mPlayerTransform = player.transform;
        else
            Debug.LogWarning("GolfBall: 'Player' 태그가 붙은 오브젝트를 찾지 못했습니다.");

        if (mInteractPromptUI != null)
            mInteractPromptUI.SetActive(false);
    }

    void Update()
    {
        if (mPlayerTransform == null) return;

        // 캐릭터 모드일 때만 근접 감지
        if (GameManager.Instance.CurrentMode != GameManager.GameMode.Character)
        {
            SetPrompt(false);
            return;
        }

        float dist = Vector3.Distance(transform.position, mPlayerTransform.position);
        bool inRange = dist <= mInteractRadius;

        if (inRange != mPlayerInRange)
        {
            mPlayerInRange = inRange;
            SetPrompt(mPlayerInRange);
        }

        if (mPlayerInRange && Input.GetKeyDown(KeyCode.F))
        {
            GameManager.Instance.EnterGolfMode();
        }
    }

    private void SetPrompt(bool visible)
    {
        if (mInteractPromptUI != null)
            mInteractPromptUI.SetActive(visible);
    }

    // 씬 뷰에서 상호작용 반경 시각화
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, mInteractRadius);
    }
}
