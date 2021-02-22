using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class PlayerController : MonoBehaviour {
    public enum EPlayerState {
        EDummy,
        ENormal,
        EJumping
    }

    // Ground Velocity
    private const float m_RunSpeed          = 4f;
    private const float m_GroundDamping     = 10f;
    private const float m_InAirDamping      = 10f;

    // Jumping Stuff
    private const float m_JumpPeakHeight                = 2.5f;
    private const float m_HorizontalDistanceToJumpPeak  = 2f;
    private const float m_JumpCutValue                  = 0.35f;
    private const float m_TerminalVelocity              = -25.0f;
    private const float m_DownGravityMultiplier         = 3f;
    private const float JUMP_PRESSED_REMEMBER_TIME      = 0.25f;
    private const float GROUNDED_REMEMBER_TIME          = 0.25f;

    private float GoingUpGravity;
    private float GoingDownGravity;
    private float JumpInitialVelocity;

    private Vector3 m_Velocity;
    private float m_Gravity;
    private float m_JumpPressedRemember;
    private float m_GroundedRemember;
    private Mover m_CharacterMover;

    private float m_NormalizedHorizontalSpeed = 0.0f;
    private EPlayerState m_CurrentPlayerState = EPlayerState.ENormal;

    // ANIMATION NAMES
    private string IDLE_ANIMATION   = "Captain";
    private string RUN_ANIMATION    = "Run";
    private string JUMP_ANIMATION   = "Jump";

    private void Awake() {
        GoingUpGravity = (-(2 * m_JumpPeakHeight * m_RunSpeed * m_RunSpeed)) / (m_HorizontalDistanceToJumpPeak * m_HorizontalDistanceToJumpPeak);
        GoingDownGravity = GoingUpGravity * m_DownGravityMultiplier;
        JumpInitialVelocity = ((2 * m_JumpPeakHeight * m_RunSpeed) / m_HorizontalDistanceToJumpPeak);
        m_CharacterMover = GetComponent<Mover>();

        m_CharacterMover.OnControllerCollidedEvent += OnControllerCollider;
        m_CharacterMover.OnTriggerEnterEvent += EnteredTrigger;
    }

    private void Start() {
        m_Gravity = GoingUpGravity;
        m_CurrentPlayerState = EPlayerState.ENormal;
    }

    void EnteredTrigger(Collider2D other) {
        // ...
    }

    void OnControllerCollider(RaycastHit2D hit) {
        // ...
    }

    private void Update() {
        Debug.Log($"Velocity: {m_Velocity}");

        m_GroundedRemember -= Time.deltaTime;
        m_JumpPressedRemember -= Time.deltaTime;
        m_NormalizedHorizontalSpeed = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space)) {
            m_JumpPressedRemember = JUMP_PRESSED_REMEMBER_TIME;
        }

        if (m_CharacterMover.IsGrounded) {
            m_GroundedRemember = GROUNDED_REMEMBER_TIME;
            m_Velocity.y = 0.0f;
        }

        switch (m_CurrentPlayerState) {
            case EPlayerState.ENormal:
                if (m_JumpPressedRemember >= 0.0f && m_GroundedRemember >= 0.0f) {
                    m_JumpPressedRemember = 0;
                    m_GroundedRemember = 0;

                    m_Gravity = GoingUpGravity;
                    m_Velocity.y = JumpInitialVelocity;
                    m_CurrentPlayerState = EPlayerState.EJumping;
                }
                break;
            case EPlayerState.EJumping:
                if (Input.GetKeyUp(KeyCode.Space) && m_Velocity.y > 0) {
                    m_Velocity.y *= m_JumpCutValue;
                }

                if (m_CharacterMover.IsGrounded) {
                    m_CurrentPlayerState = EPlayerState.ENormal;
                }
                break;
        }

        ProcessSpriteScale();
        ProcessAnimation();

        if (m_Velocity.y < 0.0f) {
            m_Gravity = GoingDownGravity;
            m_CurrentPlayerState = EPlayerState.EJumping;
        }

        float SmoothedMovementFactor = m_CharacterMover.IsGrounded ? m_GroundDamping : m_InAirDamping;

        // force removing momentum when player is not pressing any key
        if (m_NormalizedHorizontalSpeed == 0) {
            SmoothedMovementFactor *= 2.5f;
        }

        m_Velocity.x = Mathf.Lerp(m_Velocity.x, m_NormalizedHorizontalSpeed * m_RunSpeed, SmoothedMovementFactor * Time.deltaTime);
        m_Velocity.y = Mathf.Max(m_TerminalVelocity, (m_Velocity.y + (m_Gravity * Time.deltaTime)));

        Vector2 VerletVelocity = new Vector2(m_Velocity.x, m_Velocity.y + (0.5f * m_Gravity * Time.deltaTime * Time.deltaTime));
        Vector2 VerletDeltaMovement = VerletVelocity * Time.deltaTime;
        m_CharacterMover.Move(VerletDeltaMovement);
        m_Velocity = m_CharacterMover.GetVelocity();
    }

    private void ProcessSpriteScale() {
        if (m_NormalizedHorizontalSpeed == 0 && Mathf.Abs(m_Velocity.x) < Mathf.Epsilon) {
            return;
        }

        float signal = Mathf.Abs(m_NormalizedHorizontalSpeed) > Mathf.Abs(m_Velocity.x) ?
            Mathf.Sign(m_NormalizedHorizontalSpeed) :
            Mathf.Sign(m_Velocity.x);

        transform.localScale = new Vector3(signal * Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    private void ProcessAnimation() {
        // cache this reference when the game begins
        Animator m_Animator = GetComponentInChildren<Animator>();

        if (Mathf.Abs(m_Velocity.y) > Mathf.Epsilon) {
            m_Animator.Play(JUMP_ANIMATION);
        } else if (Mathf.Abs(m_NormalizedHorizontalSpeed) > 0.1f) {
            m_Animator.Play(RUN_ANIMATION);
        } else {
            m_Animator.Play(IDLE_ANIMATION);
        }
    }
}