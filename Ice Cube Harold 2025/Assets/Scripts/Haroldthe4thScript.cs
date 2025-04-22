using System.Collections;
using UnityEngine;

public class Haroldthe4thScript : MonoBehaviour
{
    [Header("Stats")]
    public PlayerStats stats;

    [Header("References")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public TrailRenderer trailRenderer;
    public Transform respawnPoint;

    [Header("Camera")]
    [SerializeField] private GameObject followCameraObject;

    [Header("Attack")]
    public Transform attackPoint;         // assign in Inspector
    public float attackRange = 0.5f;
    public int attackDamage = 1;
    public LayerMask enemyLayers;       // layer(s) your enemies sit on

    // internal cooldown/state
    private bool isAttacking = false;


    // Component references
    private Rigidbody2D rb;
    private Animator animator;
   // fix later private FollowCameraObject cameraController;
    private float fallSpeedYDampingChangeThreshold;

    // Movement state
    private float horizontal;
    private float vertical;
    private bool canMove = true;
    private bool canMoveHorizontal = true;
    private bool isFacingRight = true;

    // Jump state
    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private float jumpButtonHoldTime;
    private float jumpButtonPressTime;
    private bool isHoldingJumpButton;

    // Wall state
    private bool isTouchingLeftWall;
    private bool isTouchingRightWall;
    private bool isWallSliding;
    private bool isWallJumping;
    private float wallJumpDirection;

    // Dash state
    private int dashesAvailable = 0;
    private bool canRegularDash = true;
    private float dashCooldownTimer = 0f;

    // Knockback state
    private float knockbackCounter;
    private bool knockFromRight;

    private void Start()
    {
        if (stats == null)
        {
            Debug.LogError("PlayerStats not assigned to player controller!", this);
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        /*  // Camera setup
          if (followCameraObject != null)
          {
              cameraController = followCameraObject.GetComponent<FollowCameraObject>();
              if (CameraManager.instance != null)
              {
                  fallSpeedYDampingChangeThreshold = CameraManager.instance._fallSpeedYDampingChangeThreshold;
              }
          } */
    }

    private void Update()
    {
        if (!canMove) return;

        // Handle knockback
        if (knockbackCounter > 0)
        {
            HandleKnockback();
            return;
        }

        // Get input
        horizontal = Input.GetAxisRaw("Horizontal");
        vertical = Input.GetAxisRaw("Vertical");

        // Check ground state
        isGrounded = IsGrounded();

        // Reset dashes when landing
        if (isGrounded && !wasGroundedLastFrame)
        {
            if (dashesAvailable == 0)
            {
                dashesAvailable = 1;
            }
        }

        // Update camera damping based on fall speed
        // UpdateCameraDamping();

        // Handle jumps
        HandleJumpInput();

        // Handle wall interactions
        CheckWallContacts();
        HandleWallSliding();
        HandleWallJump();

        // Handle dash cooldown
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
            canRegularDash = false;
        }
        else
        {
            canRegularDash = true;
        }

        // Handle dash input
        if (Input.GetButtonDown("Fire1") && dashesAvailable > 0)
        {
            ExecuteDash();
        }

        if (Input.GetButtonDown("Fire2") && !isAttacking)
        {
            StartCoroutine(Attack());
        }

        // Update animation
        UpdateAnimation();

        // Handle character facing
        UpdateFacing();

        wasGroundedLastFrame = isGrounded;
    }

    private void FixedUpdate()
    {
        if (!canMove) return;

        if (canMoveHorizontal && !isWallJumping)
        {
            ApplyMovementForces();
        }
    }

    private void ApplyMovementForces()
    {
        float targetSpeed = horizontal * stats.moveSpeed;
        float speedDifference = targetSpeed - rb.linearVelocity.x;
        float accelerationRate = (Mathf.Abs(targetSpeed) > 0.01f) ? stats.acceleration : stats.deceleration;
        float movement = Mathf.Pow(Mathf.Abs(speedDifference) * accelerationRate, stats.velocityPower) * Mathf.Sign(speedDifference);

        rb.AddForce(movement * Vector2.right);
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, 0.45f, groundLayer);
    }

    private void UpdateFacing()
    {
        if ((isFacingRight && horizontal < 0f) || (!isFacingRight && horizontal > 0f))
        {
            isFacingRight = !isFacingRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;

           /* // Update camera facing
            if (cameraController != null)
            {
                cameraController.CallFlip();
            } */
        }
    }

    private void UpdateAnimation()
    {
        animator.SetFloat("Speed", Mathf.Abs(horizontal));
        animator.SetBool("IsAttack", Input.GetKeyDown(KeyCode.O));
    }

    /*private void UpdateCameraDamping()
    {
        if (cameraController == null) return;
        // If falling fast enough, increase y damping
        if (rb.velocity.y < fallSpeedYDampingChangeThreshold &&
            !cameraController.IsLerpingYDamping &&
            !cameraController.LerpedFromPlayerFalling)
        {
            cameraController.LerpYDamping(true);
        }
        // If moving up or stopped, reset y damping
        if (rb.velocity.y >= 0f &&
            !cameraController.IsLerpingYDamping &&
            cameraController.LerpedFromPlayerFalling)
        {
            cameraController.LerpedFromPlayerFalling = false;
            cameraController.LerpYDamping(false);
        }
    } 
    private void UpdateCameraDamping()
    {
        if (CameraManager.instance == null) return;

        // If falling fast enough, increase y damping
        if (rb.velocity.y < fallSpeedYDampingChangeThreshold &&
            !CameraManager.instance.IsLerpingYDamping &&
            !CameraManager.instance.LerpedFromPlayerFalling)
        {
            CameraManager.instance.LerpYDamping(true);
        }

        // If moving up or stopped, reset y damping
        if (rb.velocity.y >= 0f &&
            !CameraManager.instance.IsLerpingYDamping &&
            CameraManager.instance.LerpedFromPlayerFalling)
        {
            CameraManager.instance.LerpedFromPlayerFalling = false;
            CameraManager.instance.LerpYDamping(false);
        }
    } */

    private void HandleJumpInput()
    {
        // Normal jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpingPower);
        }

        // Variable jump height
        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * stats.jumpCutMultiplier);
        }

        // Track jump button press time for wall jumps
        if (Input.GetButtonDown("Jump"))
        {
            jumpButtonPressTime = Time.time;
            isHoldingJumpButton = true;
            rb.gravityScale = stats.fallingGravityScale; // Higher gravity while jumping
        }

        if (Input.GetButtonUp("Jump"))
        {
            isHoldingJumpButton = false;
            jumpButtonHoldTime = Time.time - jumpButtonPressTime;
        }
    }

    private void CheckWallContacts()
    {
        isTouchingLeftWall = Physics2D.OverlapBox(
            new Vector2(transform.position.x - 0.65f, transform.position.y),
            new Vector2(0.2f, 0.6f), 0f, groundLayer);

        isTouchingRightWall = Physics2D.OverlapBox(
            new Vector2(transform.position.x + 0.65f, transform.position.y),
            new Vector2(0.2f, 0.6f), 0f, groundLayer);

        if (isTouchingLeftWall)
        {
            wallJumpDirection = 1f;
        }
        else if (isTouchingRightWall)
        {
            wallJumpDirection = -1f;
        }
    }

    private void HandleWallSliding()
    {
        if ((isTouchingLeftWall || isTouchingRightWall) && !isGrounded && rb.linearVelocity.y < -0.1f)
        {
            isWallSliding = true;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y * stats.wallSlideSpeed);
            canMoveHorizontal = false;
        }
        else
        {
            isWallSliding = false;
            canMoveHorizontal = true;
        }
    }

    private void HandleWallJump()
    {
        // Wall jump activation
        if (Input.GetButtonDown("Jump") && (isTouchingLeftWall || isTouchingRightWall) && !isGrounded)
        {
            // Replenish dash when wall jumping
            dashesAvailable++;

            isWallJumping = true;

            // Cap jump hold time for wall jumps
            float effectiveJumpHoldTime = Mathf.Min(jumpButtonHoldTime, stats.wallJumpDuration);

            Invoke(nameof(StopWallJumping), effectiveJumpHoldTime * 1.5f);

            // Apply wall jump force
            rb.linearVelocity = new Vector2(stats.wallJumpSidewaysPower * wallJumpDirection, stats.wallJumpUpPower);
        }
    }

    private void StopWallJumping()
    {
        isWallJumping = false;
    }

    private void ExecuteDash()
    {
        // Choose dash type based on input direction
        if (vertical > 0.5f && Mathf.Abs(horizontal) > 0.1f)
        {
            StartCoroutine(UpDiagonalDash());
        }
        else if (vertical < -0.5f && Mathf.Abs(horizontal) > 0.1f)
        {
            if (!isGrounded)
            {
                StartCoroutine(DownDiagonalDash());
            }
            else if (canRegularDash)
            {
                StartCoroutine(Dash());
            }
        }
        else if (vertical > 0.4f && Mathf.Abs(horizontal) <= 0.1f)
        {
            StartCoroutine(UpDash());
        }
        else if (vertical < -0.4f && Mathf.Abs(horizontal) <= 0.1f)
        {
            StartCoroutine(DownDash());
        }
        else if (canRegularDash)
        {
            StartCoroutine(Dash());
        }
    }

    private IEnumerator Dash()
    {
        // Setup dash
        canMove = false;
        dashesAvailable = 0;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = stats.dashGravityScale;

        // Apply dash force
        rb.linearVelocity = new Vector2(transform.localScale.x * stats.dashingPower, 0f);

        // Visual effect
        trailRenderer.emitting = true;

        // Wait for dash duration
        yield return new WaitForSeconds(stats.dashingTime);

        // Cleanup
        trailRenderer.emitting = false;
        rb.gravityScale = originalGravity;
        canMove = true;

        // Start cooldown
        dashCooldownTimer = stats.dashingCooldown;

        // Replenish dash after use (dash chaining)
        dashesAvailable++;
    }

    private IEnumerator UpDash()
    {
        dashesAvailable = 0;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = stats.upDashGravityScale;

        rb.linearVelocity = new Vector2(0f, stats.upDashingPower);

        trailRenderer.emitting = true;
        yield return new WaitForSeconds(stats.dashingTime);
        trailRenderer.emitting = false;
        rb.gravityScale = originalGravity;

        yield return new WaitForSeconds(stats.dashingCooldown);
    }

    private IEnumerator DownDash()
    {
        dashesAvailable = 0;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = stats.dashGravityScale;

        rb.linearVelocity = new Vector2(0f, stats.downDashingPower);

        trailRenderer.emitting = true;
        yield return new WaitForSeconds(stats.dashingTime);
        trailRenderer.emitting = false;
        rb.gravityScale = originalGravity;

        yield return new WaitForSeconds(stats.dashingCooldown);
    }

    private IEnumerator UpDiagonalDash()
    {
        dashesAvailable = 0;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = stats.upDashGravityScale;

        rb.linearVelocity = new Vector2(transform.localScale.x * stats.dashingPower, stats.diagonalDashingPower);

        trailRenderer.emitting = true;
        yield return new WaitForSeconds(stats.dashingTime);
        trailRenderer.emitting = false;
        rb.gravityScale = originalGravity;

        yield return new WaitForSeconds(stats.dashingCooldown);
    }

    private IEnumerator DownDiagonalDash()
    {
        dashesAvailable = 0;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = stats.dashGravityScale;

        rb.linearVelocity = new Vector2(transform.localScale.x * stats.dashingPower, stats.diagonalDownDashingPower);

        trailRenderer.emitting = true;
        yield return new WaitForSeconds(stats.dashingTime);
        trailRenderer.emitting = false;
        rb.gravityScale = originalGravity;

        yield return new WaitForSeconds(stats.dashingCooldown);
    }

    private IEnumerator Attack()
    {
        isAttacking = true;
        canMove = false;            // freeze movement
        animator.SetTrigger("Attack");  // you need an “Attack” trigger in your Animator

        // wait until the “hit frame” of your animation (tweak to match your clip)
        yield return new WaitForSeconds(0.1f);

        // detect enemies in range of the swing
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
            attackPoint.position,
            attackRange,
            enemyLayers
        );
        foreach (var enemy in hitEnemies)
        {
            // assumes your enemies have a script with a TakeDamage(int) method
            enemy.GetComponent<Enemy>()?.TakeDamage(attackDamage);
        }

        // wait for the rest of the swing animation
        yield return new WaitForSeconds(0.3f);

        canMove = true;
        isAttacking = false;
    }



    private void HandleKnockback()
    {
        if (knockbackCounter <= 0)
        {
            canMove = true;
            return;
        }

        // Apply knockback force
        float knockbackX = knockFromRight ? -stats.knockbackForce : stats.knockbackForce;
        rb.linearVelocity = new Vector2(knockbackX, stats.knockbackForce);

        knockbackCounter -= Time.deltaTime;
    }

    public void ApplyKnockback(bool fromRight, float durationMultiplier = 1f)
    {
        knockbackCounter = stats.knockbackDuration * durationMultiplier;
        knockFromRight = fromRight;
        canMove = false;
    }

    /*
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Death"))
        {
            transform.position = respawnPoint.position;
        }
    } */

    private void OnDrawGizmosSelected()
    {
        // Wall detection boxes
        Gizmos.color = Color.blue;
        Gizmos.DrawCube(new Vector2(transform.position.x - 0.65f, transform.position.y), new Vector2(0.2f, 0.6f));
        Gizmos.DrawCube(new Vector2(transform.position.x + 0.65f, transform.position.y), new Vector2(0.2f, 0.6f));

        // Ground check ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, Vector2.down * 0.5f);
    }
}