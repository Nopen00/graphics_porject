using UnityEngine;

public static class CustomPhysicsEngine
{
    // 중력 가속도 상수 (유니티 내장 값을 쓰되, 커스텀 가속도로 연산)
    public static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
    
    // 바람이 공에 미치는 영향력 계수 (실제 체감 물리량 조절용)
    public static float WindInfluenceCoefficient = 0.1f;

    /// <summary>
    /// 매 프레임 공의 다음 속도를 계산합니다. (바람 + 중력 반영)
    /// </summary>
    public static Vector3 CalculateNextVelocity(Vector3 currentVelocity, Vector3 windVector, float deltaTime)
    {
        // 1. 가속도 합성: 중력 + (바람 벡터 * 바람 저항 계수)
        Vector3 totalAcceleration = Gravity + (windVector * WindInfluenceCoefficient);
        
        // 2. 속도 업데이트: v = v0 + a * dt
        Vector3 nextVelocity = currentVelocity + (totalAcceleration * deltaTime);
        
        return nextVelocity;
    }

    /// <summary>
    /// 매 프레임 공의 다음 위치를 계산합니다.
    /// </summary>
    public static Vector3 CalculateNextPosition(Vector3 currentPosition, Vector3 nextVelocity, float deltaTime)
    {
        // 3. 위치 업데이트: p = p0 + v * dt
        Vector3 nextPosition = currentPosition + (nextVelocity * deltaTime);
        
        return nextPosition;
    }
}