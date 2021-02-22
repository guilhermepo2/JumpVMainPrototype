#define DEBUG_RAYS
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class Mover : MonoBehaviour {
    [System.Serializable]
    protected struct RaycastOrigins {
        public Vector3 TopLeft;
        public Vector3 TopRight;
        public Vector3 BottomRight;
        public Vector3 BottomLeft;
    }

    [System.Serializable]
    public class CollisionState {
        public bool CollisionAbove;
        public bool CollisionRight;
        public bool CollisionBelow;
        public bool CollisionLeft;
        public bool BecameGroundedThisFrame;
        public bool WasGroundedLastFrame;

        public bool HasCollision() {
            return (CollisionAbove || CollisionRight || CollisionBelow || CollisionLeft);
        }

        public void ResetCollision() {
            CollisionAbove = CollisionRight = CollisionBelow = CollisionLeft = BecameGroundedThisFrame = false;
        }
    }

    public event Action<RaycastHit2D> OnControllerCollidedEvent;
    public event Action<Collider2D> OnTriggerEnterEvent;
    public event Action<Collider2D> OnTriggerStayEvent;
    public event Action<Collider2D> OnTriggerExitEvent;

    protected bool IgnoreOneWayPlatformThisFrame;
    private float m_SkinWidth = 0.0625f;
    public float SkinWidth {
        get {
            return m_SkinWidth;
        }
        set {
            m_SkinWidth = value;
            RecalculateDistanceBetweenRays();
        }
    }

    public LayerMask PlatformMask = 0;
    public LayerMask TriggerMask = 0;
    public LayerMask OneWayPlatformMask = 0;

    private const int TOTAL_HORIZONTAL_RAYS = 6;
    private const int TOTAL_VERTICAL_RAYS = 4;

    private BoxCollider2D m_BoxCollider;
    private Rigidbody2D m_Rigidbody;
    protected CollisionState m_CollisionState = new CollisionState();
    public bool IsGrounded { get { return m_CollisionState.CollisionBelow; } }
    public bool WasGroundedLastFrame { get { return m_CollisionState.WasGroundedLastFrame; } }
    private Vector3 m_Velocity;
    public Vector3 GetVelocity() { return m_Velocity; }
    
    private const float SKIN_FUDGE_FACTOR = 0.001f;

    protected RaycastOrigins m_RaycastOrigins;
    private RaycastHit2D m_RaycastHit;
    List<RaycastHit2D> m_RaycastHitsThisFrame = new List<RaycastHit2D>(2);

    private float m_VerticalDistanceBetweenRays;
    private float m_HorizontalDistanceBetweenRays;

    public void OnTriggerEnter2D(Collider2D collision) {
        OnTriggerEnterEvent?.Invoke(collision);
    }

    public void OnTriggerStay2D(Collider2D collision) {
        OnTriggerStayEvent?.Invoke(collision);
    }

    public void OnTriggerExit2D(Collider2D collision) {
        OnTriggerExitEvent?.Invoke(collision);
    }

    private void Awake() {
        PlatformMask |= OneWayPlatformMask;
        m_BoxCollider = GetComponent<BoxCollider2D>();
        m_Rigidbody = GetComponent<Rigidbody2D>();


        SkinWidth = m_SkinWidth;
        for (int i = 0; i < 32; i++) {
            if ((TriggerMask.value & 1 << i) == 0) {
                Physics2D.IgnoreLayerCollision(gameObject.layer, i);
            }
        }
    }

    private void OnValidate() {
        Rigidbody2D tempRigidbody = GetComponent<Rigidbody2D>();

        if (tempRigidbody != null) {
            tempRigidbody.isKinematic = true;
            tempRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            tempRigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;
            tempRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    private void RecalculateDistanceBetweenRays() {
        float ColliderUseableHeight = m_BoxCollider.size.y - (2f * m_SkinWidth);
        float ColliderUseableWidth = m_BoxCollider.size.x - (2f * m_SkinWidth);

        m_VerticalDistanceBetweenRays = ColliderUseableHeight / (TOTAL_HORIZONTAL_RAYS - 1);
        m_HorizontalDistanceBetweenRays = ColliderUseableWidth / (TOTAL_VERTICAL_RAYS - 1);
    }

    private void CalculateRaycastOrigins() {
        Bounds ModifiedBounds = m_BoxCollider.bounds;
        ModifiedBounds.Expand(-2f * m_SkinWidth);

        m_RaycastOrigins.TopRight = ModifiedBounds.max;
        m_RaycastOrigins.TopLeft = new Vector2(ModifiedBounds.min.x, ModifiedBounds.max.y);
        m_RaycastOrigins.BottomRight = new Vector2(ModifiedBounds.max.x, ModifiedBounds.min.y);
        m_RaycastOrigins.BottomLeft = ModifiedBounds.min;
    }

    [System.Diagnostics.Conditional("DEBUG_RAYS")]
    void DrawRay(Vector3 start, Vector3 dir, Color color) {
        Debug.DrawRay(start, dir, color);
    }

    public void Move(Vector3 DeltaMovement) {
        m_CollisionState.WasGroundedLastFrame = m_CollisionState.CollisionBelow;
        m_CollisionState.ResetCollision();
        m_RaycastHitsThisFrame.Clear();
        CalculateRaycastOrigins();

        if (DeltaMovement.x != 0f) {
            MoveHorizontal(ref DeltaMovement);
        }

        if (DeltaMovement.y != 0f) {
            MoveVertical(ref DeltaMovement);
        }

        DeltaMovement.z = 0f;
        transform.Translate(DeltaMovement, Space.World);

        if (Time.deltaTime > 0f) {
            m_Velocity = DeltaMovement / Time.deltaTime;
        }

        if (!m_CollisionState.WasGroundedLastFrame && m_CollisionState.CollisionBelow) {
            m_CollisionState.BecameGroundedThisFrame = true;
        }

        if (OnControllerCollidedEvent != null) {
            for (int i = 0; i < m_RaycastHitsThisFrame.Count; i++) {
                OnControllerCollidedEvent(m_RaycastHitsThisFrame[i]);
            }
        }

        IgnoreOneWayPlatformThisFrame = false;
    }

    private void MoveHorizontal(ref Vector3 DeltaMovement) {
        bool IsGoingRight = DeltaMovement.x > 0;
        float RayDistance = Mathf.Abs(DeltaMovement.x) + m_SkinWidth;
        Vector2 RayDirection = IsGoingRight ? Vector2.right : Vector2.left;
        Vector2 InitialRayOrigin = IsGoingRight ? m_RaycastOrigins.BottomRight : m_RaycastOrigins.BottomLeft;

        for (int i = 0; i < TOTAL_HORIZONTAL_RAYS; i++) {
            Vector2 ray = new Vector2(InitialRayOrigin.x, InitialRayOrigin.y + i * m_VerticalDistanceBetweenRays);
            DrawRay(ray, RayDirection * RayDistance, Color.red);

            if (i == 0 && m_CollisionState.WasGroundedLastFrame) {
                m_RaycastHit = Physics2D.Raycast(ray, RayDirection, RayDistance, PlatformMask);
            } else {
                m_RaycastHit = Physics2D.Raycast(ray, RayDirection, RayDistance, PlatformMask & ~OneWayPlatformMask);
            }

            if (m_RaycastHit) {
                DeltaMovement.x = m_RaycastHit.point.x - ray.x;
                RayDistance = Mathf.Abs(DeltaMovement.x);

                if (IsGoingRight) {
                    DeltaMovement.x -= m_SkinWidth;
                    m_CollisionState.CollisionRight = true;
                } else {
                    DeltaMovement.x += m_SkinWidth;
                    m_CollisionState.CollisionLeft = true;
                }
            }

            m_RaycastHitsThisFrame.Add(m_RaycastHit);

            if (RayDistance < m_SkinWidth + SKIN_FUDGE_FACTOR) {
                break;
            }
        }
    }

    private void MoveVertical(ref Vector3 DeltaMovement) {
        bool IsGoingUp = DeltaMovement.y > 0;
        float RayDistance = Mathf.Abs(DeltaMovement.y) + m_SkinWidth;
        Vector2 RayDirection = IsGoingUp ? Vector2.up : Vector2.down;
        Vector2 InitialRayOrigin = IsGoingUp ? m_RaycastOrigins.TopLeft : m_RaycastOrigins.BottomLeft;

        InitialRayOrigin.x += DeltaMovement.x;

        var Mask = PlatformMask;
        if (IsGoingUp && !m_CollisionState.WasGroundedLastFrame) {
            Mask &= ~OneWayPlatformMask;
        }

        for (int i = 0; i < TOTAL_VERTICAL_RAYS; i++) {
            var Ray = new Vector2(InitialRayOrigin.x + i * m_HorizontalDistanceBetweenRays, InitialRayOrigin.y);
            DrawRay(Ray, RayDirection * RayDistance, Color.red);

            m_RaycastHit = Physics2D.Raycast(Ray, RayDirection, RayDistance, Mask);

            if (m_RaycastHit) {
                DeltaMovement.y = m_RaycastHit.point.y - Ray.y;
                RayDistance = Mathf.Abs(DeltaMovement.y);

                if (IsGoingUp) {
                    DeltaMovement.y -= m_SkinWidth;
                    m_CollisionState.CollisionAbove = true;
                } else {
                    DeltaMovement.y += m_SkinWidth;
                    m_CollisionState.CollisionBelow = true;
                }

                m_RaycastHitsThisFrame.Add(m_RaycastHit);

                if (RayDistance < m_SkinWidth + SKIN_FUDGE_FACTOR) {
                    break;
                }
            }
        }
    }
}