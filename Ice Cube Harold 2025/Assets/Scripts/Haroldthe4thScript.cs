using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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

    //Player Controls
    private PlayerControls controls;
    private Vector2 moveInput;

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




    public void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Gameplay.Enable();
        controls.Gameplay.Move.performed += OnMovePerformed;
        controls.Gameplay.Move.canceled += OnMoveCanceled;
        
        // Set up jump controls
        controls.Gameplay.Jump.performed += OnJumpPerformed;
        controls.Gameplay.Jump.canceled += OnJumpCanceled;
        
        // Set up dash controls
        controls.Gameplay.Dash.performed += OnDashPerformed;

        // Set up attack controls
        controls.Gameplay.Attack.performed += OnAttackPerformed;

    }

    private void OnDisable()
    {
        controls.Gameplay.Move.performed -= OnMovePerformed;
        controls.Gameplay.Move.canceled -= OnMoveCanceled;
        controls.Gameplay.Jump.performed -= OnJumpPerformed;
        controls.Gameplay.Jump.canceled -= OnJumpCanceled;
        controls.Gameplay.Dash.performed -= OnDashPerformed;
        controls.Gameplay.Disable();
        controls.Gameplay.Attack.performed -= OnAttackPerformed;

    }

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

        // Update horizontal and vertical from moveInput
        horizontal = moveInput.x;
        vertical = moveInput.y;

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

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }
    
    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpButtonPressTime = Time.time;
        isHoldingJumpButton = true;
        
        // Normal jump
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.jumpingPower);
        }
        // Wall jump
        else if ((isTouchingLeftWall || isTouchingRightWall) && !isGrounded)
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
        
        rb.gravityScale = stats.fallingGravityScale; // Higher gravity while jumping
    }
    
    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        isHoldingJumpButton = false;
        jumpButtonHoldTime = Time.time - jumpButtonPressTime;
        
        // Variable jump height
        if (rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * stats.jumpCutMultiplier);
        }
    }
    
    private void OnDashPerformed(InputAction.CallbackContext context)
    {
        if (dashesAvailable > 0)
        {
            ExecuteDash();
        }
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (!isAttacking) // Optional cooldown logic
        {
            StartCoroutine(PerformAttack());
        }
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
        // Replace with new input system for attack later
        bool isAttacking = false;
        if (controls.Gameplay.Attack.IsPressed())
        {
            isAttacking = true;
        }
        animator.SetBool("IsAttack", isAttacking);
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

    private void CheckWallContacts()
    {
        isTouchingLeftWall = Physics2D.OverlapBox(
            new Vector2(transform.position.x - 0.5f, transform.position.y),
            new Vector2(0.2f, 0.6f), 0f, groundLayer);

        isTouchingRightWall = Physics2D.OverlapBox(
            new Vector2(transform.position.x + 0.5f, transform.position.y),
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
        // Wall jump is now handled in OnJumpPerformed
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

    private IEnumerator PerformAttack()
    {
        isAttacking = true;
        //animator.SetTrigger("Attack"); // Assuming you have an "Attack" trigger in the Animator

        // Delay to sync with animation if needed
        yield return new WaitForSeconds(0.1f); // Adjust based on your animation windup

        // Detect enemies in range
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            // Call your enemy's damage function
            enemy.GetComponent<Enemy>()?.TakeDamage(attackDamage);
        }

        yield return new WaitForSeconds(0.3f); // Optional cooldown time
        isAttacking = false;
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
        Gizmos.DrawCube(new Vector2(transform.position.x - 0.5f, transform.position.y), new Vector2(0.2f, 0.6f));
        Gizmos.DrawCube(new Vector2(transform.position.x + 0.5f, transform.position.y), new Vector2(0.2f, 0.6f));

        // Ground check ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, Vector2.down * 0.5f);

        // Attack point gizmo
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);

    }
}