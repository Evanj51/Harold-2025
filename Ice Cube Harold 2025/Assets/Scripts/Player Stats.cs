using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Game/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float acceleration = 5f;
    public float deceleration = 5f;
    public float velocityPower = 1f;

    [Header("Jumping")]
    public float jumpingPower = 16f;
    public float jumpCutMultiplier = 0.5f;
    public float fallingGravityScale = 8.17f;

    [Header("Wall Movement")]
    public float wallJumpSidewaysPower = 5f;
    public float wallJumpUpPower = 12f;
    public float wallSlideSpeed = 0.3f;
    public float wallJumpDuration = 0.075f;

    [Header("Dash")]
    public float dashingPower = 24f;
    public float dashingTime = 0.2f;
    public float dashingCooldown = 1f;
    public float upDashingPower = 24f;
    public float downDashingPower = -24f;
    public float diagonalDashingPower = 24f;
    public float diagonalDownDashingPower = -24f;
    public float dashGravityScale = 0f;
    public float upDashGravityScale = 20f;

    [Header("Knockback")]
    public float knockbackForce = 10f;
    public float knockbackDuration = 0.5f;
}