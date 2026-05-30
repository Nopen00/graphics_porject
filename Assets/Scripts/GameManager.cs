using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameMode { Character, Golf }
    public GameMode CurrentMode { get; private set; } = GameMode.Character;

    [Header("캐릭터 모드 오브젝트")]
    [SerializeField] private GameObject mCharacter;       // Astronaut 최상위 오브젝트
    [SerializeField] private Camera     mCharacterCamera; // CameraTPS가 붙은 Main Camera

    [Header("골프 모드 오브젝트")]
    [SerializeField] private Camera     mGolfCamera;      // CameraController가 붙은 골프 카메라
    [SerializeField] private GameObject mGolfUI;          // 골프 HUD (파워바, 바람 등) 루트

    void Update()
    {
        if (CurrentMode == GameMode.Golf && Input.GetKeyDown(KeyCode.Escape))
            ExitGolfMode();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // 시작 시 골프 모드 오브젝트 비활성화
        mGolfCamera.gameObject.SetActive(false);
        if (mGolfUI != null) mGolfUI.SetActive(false);
    }

    public void EnterGolfMode()
    {
        if (CurrentMode == GameMode.Golf) return;
        CurrentMode = GameMode.Golf;

        mCharacter.SetActive(false);
        mCharacterCamera.gameObject.SetActive(false);
        mGolfCamera.gameObject.SetActive(true);
        if (mGolfUI != null) mGolfUI.SetActive(true);
    }

    public void ExitGolfMode()
    {
        if (CurrentMode == GameMode.Character) return;
        CurrentMode = GameMode.Character;

        mCharacter.SetActive(true);
        mCharacterCamera.gameObject.SetActive(true);
        mGolfCamera.gameObject.SetActive(false);
        if (mGolfUI != null) mGolfUI.SetActive(false);
    }
}
