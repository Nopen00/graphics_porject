using UnityEngine;

/// OB 경계벽 — Box Collider (Is Trigger = true) 에 부착
/// 공이 트리거에 진입하면 GolfBallPhysics.OutOfBounds() 호출
public class OBWall : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        GolfBallPhysics ball = other.GetComponent<GolfBallPhysics>();
        if (ball != null)
            ball.OutOfBounds();
    }
}
