using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Движение и прыжок")]
    public float moveSpeed = 10f;
    public float jumpForce = 15f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Дэш")]
    public float dashSpeed = 24f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.1f;

    [Header("Захват и прыжки со стены")]
    public Transform wallCheck;
    public Vector2 wallCheckSize = new Vector2(0.15f, 1.1f);
    public float wallClimbSpeed = 6f;
    public float wallClimbJumpForce = 12.5f;
    public Vector2 wallJumpForce = new Vector2(11f, 14f);
    public float wallJumpLockTime = 0.16f;

    [Header("Выносливость")]
    public float maxStamina = 110f;
    public float holdStaminaDrain = 10f;
    public float climbStaminaDrain = 45f;
    public float climbJumpStaminaCost = 27.5f;
    public float sameWallJumpStaminaCost = 30f;
    public float exhaustedWallJumpYMultiplier = 0.42f;
    public float lowStaminaThreshold = 25f;
    public Color lowStaminaColor = new Color(1f, 0.749f, 0.78f, 1f);
    public Color exhaustedColor = new Color(0.478f, 0.086f, 0.125f, 1f);

    [Header("Проверка земли")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.85f, 0.15f);
    public LayerMask groundLayer;

    [Header("Палка Хакурей")]
    public SpriteRenderer goheiRenderer;
    public Color dashReadyColor = Color.white;
    public Color dashSpentColor = new Color(0.22f, 0.714f, 1f, 1f);

    [Header("Респавн")]
    public Transform spawnPoint;
    public float respawnDelay = 0.2f;

    private Rigidbody2D rb;
    private SpriteRenderer playerRenderer;
    private Animator anim;
    private Color originalPlayerColor;
    private Vector3 defaultSpawnPos;

    private float moveInputX;
    private float moveInputY;
    private float facingDirection = 1f;
    private float lastWallJumpDir;

    private bool isGrounded;
    private bool isTouchingWall;
    private bool isWallGrabbing;
    private bool pendingWallRegrab;

    private float coyoteCounter;
    private float jumpBufferCounter;
    private float wallJumpLockCounter;
    private float wallDetachCooldown;
    private float currentStamina;

    private bool canDash = true;
    private bool isDashing;
    private bool isDead;
    private float dashCooldownCounter;
    private float originalGravity;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerRenderer = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        originalGravity = rb.gravityScale;
        originalPlayerColor = playerRenderer.color;
        defaultSpawnPos = transform.position;
        currentStamina = maxStamina;
        UpdateGoheiColor();
    }

    void Update()
    {
        if (isDead) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            Die();
            return;
        }

        if (dashCooldownCounter > 0f)
            dashCooldownCounter -= Time.deltaTime;

        if (wallJumpLockCounter > 0f)
            wallJumpLockCounter -= Time.deltaTime;

        if (wallDetachCooldown > 0f)
            wallDetachCooldown -= Time.deltaTime;

        if (isDashing) return;

        moveInputX = Input.GetAxisRaw("Horizontal");
        moveInputY = Input.GetAxisRaw("Vertical");

        isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
        isTouchingWall = wallCheck != null && Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0f, groundLayer);

        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
            currentStamina = maxStamina;
            lastWallJumpDir = 0f;
            pendingWallRegrab = false;

            if (!canDash && dashCooldownCounter <= 0f)
            {
                RefillDash();
            }
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.C))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (!isWallGrabbing && moveInputX != 0 && wallJumpLockCounter <= 0f)
        {
            SetFacing(Mathf.Sign(moveInputX));
            isTouchingWall = wallCheck != null && Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0f, groundLayer);
        }

        bool pressingIntoWall = moveInputX != 0 && Mathf.Sign(moveInputX) == facingDirection;
        bool pressingAwayFromWall = moveInputX != 0 && Mathf.Sign(moveInputX) == -facingDirection;

        if (pressingAwayFromWall || !isTouchingWall)
        {
            pendingWallRegrab = false;
        }

        if (!isWallGrabbing)
        {
            bool canAutoStick = (!isGrounded && (pressingIntoWall || (pendingWallRegrab && rb.velocity.y <= wallClimbSpeed)))
                || (isGrounded && moveInputY > 0f);

            if (isTouchingWall && wallDetachCooldown <= 0f && wallJumpLockCounter <= 0f && currentStamina > 0f && canAutoStick)
            {
                isWallGrabbing = true;
                pendingWallRegrab = false;
                rb.gravityScale = 0f;
                rb.velocity = Vector2.zero;
            }
        }

        if (jumpBufferCounter > 0f)
        {
            if (isWallGrabbing)
            {
                bool wantsClimbJump = !pressingAwayFromWall && (moveInputY > 0f || pressingIntoWall) && currentStamina > 0f;
                if (wantsClimbJump)
                {
                    PerformWallClimbJump();
                }
                else
                {
                    PerformWallJump();
                }
            }
            else if (coyoteCounter > 0f)
            {
                rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
            }
            else if (isTouchingWall && !isGrounded)
            {
                bool wantsClimbJump = pendingWallRegrab && !pressingAwayFromWall && (moveInputY > 0f || pressingIntoWall) && currentStamina > 0f;
                if (wantsClimbJump)
                {
                    PerformWallClimbJump();
                }
                else
                {
                    PerformWallJump();
                }
            }
        }

        if (isWallGrabbing)
        {
            if (!isTouchingWall || (isGrounded && moveInputY <= 0f) || pressingAwayFromWall || currentStamina <= 0f)
            {
                if (currentStamina <= 0f)
                {
                    lastWallJumpDir = -facingDirection;
                }

                isWallGrabbing = false;
                rb.gravityScale = originalGravity;

                if (pressingAwayFromWall)
                {
                    wallDetachCooldown = 0.15f;
                    SetFacing(Mathf.Sign(moveInputX));
                }
            }
            else
            {
                if (moveInputY > 0f)
                {
                    currentStamina -= climbStaminaDrain * Time.deltaTime;
                }
                else if (moveInputY == 0f)
                {
                    currentStamina -= holdStaminaDrain * Time.deltaTime;
                }

                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                    lastWallJumpDir = -facingDirection;
                    isWallGrabbing = false;
                    rb.gravityScale = originalGravity;
                }
            }
        }

        if ((Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.C)) && rb.velocity.y > 0f)
        {
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * 0.5f);
        }

        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.X)) && canDash && dashCooldownCounter <= 0f)
        {
            StartCoroutine(PerformDash());
        }

        UpdateAnimations();
        UpdateStaminaVisual();
    }

    void FixedUpdate()
    {
        if (isDashing || isDead) return;

        if (isWallGrabbing)
        {
            rb.velocity = new Vector2(0f, moveInputY * wallClimbSpeed);
            return;
        }

        if (wallJumpLockCounter > 0f) return;

        rb.velocity = new Vector2(moveInputX * moveSpeed, rb.velocity.y);
    }

    private void SetFacing(float dir)
    {
        facingDirection = dir;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }

    private void PerformWallClimbJump()
    {
        isWallGrabbing = false;
        rb.gravityScale = originalGravity;
        jumpBufferCounter = 0f;
        wallJumpLockCounter = 0.12f;
        wallDetachCooldown = 0.12f;
        pendingWallRegrab = true;
        lastWallJumpDir = -facingDirection;

        currentStamina = Mathf.Max(0f, currentStamina - climbJumpStaminaCost);
        rb.velocity = new Vector2(0f, wallClimbJumpForce);
    }

    private void PerformWallJump()
    {
        isWallGrabbing = false;
        pendingWallRegrab = false;
        rb.gravityScale = originalGravity;
        jumpBufferCounter = 0f;
        wallDetachCooldown = 0.12f;

        float jumpDir = -facingDirection;
        bool isSameWall = Mathf.Approximately(jumpDir, lastWallJumpDir);
        float yMultiplier = 1f;

        if (isSameWall)
        {
            currentStamina = Mathf.Max(0f, currentStamina - sameWallJumpStaminaCost);
            float staminaRatio = maxStamina > 0f ? currentStamina / maxStamina : 0f;
            yMultiplier = Mathf.Lerp(exhaustedWallJumpYMultiplier, 0.82f, staminaRatio);
        }

        wallJumpLockCounter = (isSameWall && currentStamina <= 0f) ? wallJumpLockTime * 1.3f : wallJumpLockTime;
        lastWallJumpDir = jumpDir;
        SetFacing(jumpDir);
        rb.velocity = new Vector2(jumpDir * wallJumpForce.x, wallJumpForce.y * yMultiplier);
    }

    private IEnumerator PerformDash()
    {
        canDash = false;
        isDashing = true;
        isWallGrabbing = false;
        pendingWallRegrab = false;
        wallJumpLockCounter = 0f;
        wallDetachCooldown = 0f;
        if (anim != null) anim.SetBool("isRunning", false);
        UpdateGoheiColor();

        Vector2 dashDir = new Vector2(moveInputX, moveInputY);
        if (dashDir == Vector2.zero)
        {
            dashDir = new Vector2(facingDirection, 0f);
        }
        dashDir.Normalize();

        rb.gravityScale = 0f;
        rb.velocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        rb.gravityScale = originalGravity;
        rb.velocity *= 0.4f;
        isDashing = false;
        dashCooldownCounter = dashCooldown;
    }

    public bool CanDash => canDash;
    public bool NeedsRefill => !canDash || currentStamina < maxStamina;

    public void RefillDash()
    {
        canDash = true;
        currentStamina = maxStamina;
        lastWallJumpDir = 0f;
        dashCooldownCounter = 0f;
        UpdateGoheiColor();
        UpdateStaminaVisual();
    }

    public void Die()
    {
        if (isDead) return;
        StopAllCoroutines();
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        isDead = true;
        isDashing = false;
        isWallGrabbing = false;
        pendingWallRegrab = false;
        wallJumpLockCounter = 0f;
        wallDetachCooldown = 0f;
        lastWallJumpDir = 0f;
        if (anim != null) anim.SetBool("isRunning", false);

        rb.velocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.simulated = false;
        playerRenderer.enabled = false;
        if (goheiRenderer != null) goheiRenderer.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        transform.position = spawnPoint != null ? spawnPoint.position : defaultSpawnPos;
        rb.simulated = true;
        rb.gravityScale = originalGravity;
        playerRenderer.enabled = true;
        if (goheiRenderer != null) goheiRenderer.enabled = true;

        RefillDash();
        isDead = false;
    }

    private void UpdateAnimations()
    {
        if (anim == null) return;

        bool isRunning = isGrounded && !isWallGrabbing && !isDashing && Mathf.Abs(moveInputX) > 0.01f;
        anim.SetBool("isRunning", isRunning);
    }

    private void UpdateGoheiColor()
    {
        if (goheiRenderer != null)
        {
            goheiRenderer.color = canDash ? dashReadyColor : dashSpentColor;
        }
    }

    private void UpdateStaminaVisual()
    {
        if (playerRenderer == null || isDead) return;

        if (currentStamina <= 0f)
        {
            playerRenderer.color = exhaustedColor;
        }
        else if (currentStamina <= lowStaminaThreshold)
        {
            bool blink = Mathf.PingPong(Time.time * 14f, 1f) > 0.5f;
            playerRenderer.color = blink ? lowStaminaColor : originalPlayerColor;
        }
        else
        {
            playerRenderer.color = originalPlayerColor;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }

        if (wallCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(wallCheck.position, wallCheckSize);
        }
    }
}