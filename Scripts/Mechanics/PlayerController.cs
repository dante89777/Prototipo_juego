using Platformer.Core;
using Platformer.Gameplay;
using Platformer.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Estados posibles del salto del jugador.
    /// </summary>
    public enum PlayerJumpState
    {
        Grounded,
        PrepareToJump,
        Jumping,
        InFlight,
        Landed
    }

    /// <summary>
    /// This is the main class used to implement control of the player.
    /// It is a superset of the AnimationController class, but is inlined to
    /// allow for any kind of customisation.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(SpriteRenderer), typeof(Animator))]
    public class PlayerController : KinematicObject
    {
        [Header("Sonidos")]
        [Tooltip("Audio played when the player jumps.")]
        [SerializeField] private AudioClip jumpAudio;
        [Tooltip("Audio played when the player respawns.")]
        [SerializeField] private AudioClip respawnAudio;
        [Tooltip("Audio played when the player gets hurt.")]
        [SerializeField] private AudioClip ouchAudio;

        [Header("Gancho")]
        [Tooltip("Audio played when the hook is fired.")]
        [SerializeField] private AudioClip hookAudio;
        [Tooltip("How far the hook can reach.")]
        [SerializeField] private float hookRange = 8f;
        [Tooltip("How fast the player is pulled towards the hook.")]
        [SerializeField] private float hookPullSpeed = 12f;
        [Tooltip("Distance at which the player is considered to have reached the hook.")]
        [SerializeField] private float hookStopDistance = 1.2f;
        [Tooltip("Layers the hook can attach to.")]
        [SerializeField] private LayerMask hookableLayers = ~0;

        [Header("Movimiento")]
        [Tooltip("Max horizontal speed of the player.")]
        [SerializeField] private float maxSpeed = 7;
        [Tooltip("Initial jump velocity at the start of a jump.")]
        [SerializeField] private float jumpTakeOffSpeed = 7;

        /// <summary>
        /// Max horizontal speed of the player.
        /// </summary>
        public float MaxSpeed
        {
            get { return maxSpeed; }
            set { maxSpeed = value; }
        }

        /// <summary>
        /// Audio played when the player jumps.
        /// </summary>
        public AudioClip JumpAudio => jumpAudio;

        /// <summary>
        /// Audio played when the player respawns.
        /// </summary>
        public AudioClip RespawnAudio => respawnAudio;

        /// <summary>
        /// Audio played when the player gets hurt.
        /// </summary>
        public AudioClip OuchAudio => ouchAudio;

        /// <summary>
        /// The collider used by the movement system.
        /// </summary>
        public Collider2D Collider => collider2d;

        /// <summary>
        /// The audio source used for one shot sounds.
        /// </summary>
        public AudioSource AudioSource => audioSource;

        /// <summary>
        /// The health of the player.
        /// </summary>
        public Health Health => health;

        /// <summary>
        /// The animator of the player.
        /// </summary>
        public Animator Animator => animator;

        /// <summary>
        /// Current state of the jump.
        /// </summary>
        public PlayerJumpState JumpState { get; set; } = PlayerJumpState.Grounded;

        /// <summary>
        /// True when the player accepts input.
        /// </summary>
        public bool ControlEnabled { get; set; } = true;

        bool jump;
        bool stopJump;
        Vector2 move;
        SpriteRenderer spriteRenderer;
        Health health;
        AudioSource audioSource;
        Collider2D collider2d;
        Animator animator;

        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        InputAction m_MoveAction;
        InputAction m_JumpAction;
        InputAction m_HookAction;

        bool grappling;
        Vector2 grapplePoint;
        LineRenderer rope;

        /// <summary>
        /// Bounds of the player's collider.
        /// </summary>
        public Bounds Bounds => collider2d.bounds;

        protected override void OnEnable()
        {
            base.OnEnable();
            health = GetComponent<Health>();
            audioSource = GetComponent<AudioSource>();
            collider2d = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
            m_HookAction = InputSystem.actions.FindAction("Player/Hook");

            if (m_MoveAction != null) m_MoveAction.Enable();
            if (m_JumpAction != null) m_JumpAction.Enable();
            if (m_HookAction != null) m_HookAction.Enable();

            CreateRope();
        }

        protected override void Update()
        {
            if (ControlEnabled)
            {
                if (m_MoveAction != null)
                    move.x = m_MoveAction.ReadValue<Vector2>().x;

                if (m_JumpAction != null)
                {
                    if (JumpState == PlayerJumpState.Grounded && m_JumpAction.WasPressedThisFrame())
                        JumpState = PlayerJumpState.PrepareToJump;
                    else if (m_JumpAction.WasReleasedThisFrame())
                    {
                        stopJump = true;
                        Schedule<PlayerStopJump>().player = this;
                    }
                }

                if (m_HookAction != null && model.GrappleUnlocked)
                {
                    if (m_HookAction.WasPressedThisFrame())
                        TryStartHook();
                    else if (m_HookAction.WasReleasedThisFrame())
                        StopHook();
                }
            }
            else
            {
                move.x = 0;
            }

            UpdateJumpState();
            base.Update();
        }

        void UpdateJumpState()
        {
            jump = false;
            switch (JumpState)
            {
                case PlayerJumpState.PrepareToJump:
                    JumpState = PlayerJumpState.Jumping;
                    jump = true;
                    stopJump = false;
                    break;
                case PlayerJumpState.Jumping:
                    if (!IsGrounded)
                    {
                        Schedule<PlayerJumped>().player = this;
                        JumpState = PlayerJumpState.InFlight;
                    }
                    break;
                case PlayerJumpState.InFlight:
                    if (IsGrounded)
                    {
                        Schedule<PlayerLanded>().player = this;
                        JumpState = PlayerJumpState.Landed;
                    }
                    break;
                case PlayerJumpState.Landed:
                    JumpState = PlayerJumpState.Grounded;
                    break;
            }
        }

        protected override void ComputeVelocity()
        {
            if (grappling)
            {
                var toPoint = grapplePoint - (Vector2)transform.position;
                if (toPoint.magnitude <= hookStopDistance || !ControlEnabled)
                {
                    StopHook();
                }
                else
                {
                    var dir = toPoint.normalized;
                    targetVelocity = dir * hookPullSpeed;
                    velocity.y = dir.y * hookPullSpeed;
                    UpdateAnimation();
                    return;
                }
            }

            if (jump && IsGrounded)
            {
                velocity.y = jumpTakeOffSpeed * model.jumpModifier;
                jump = false;
                if (animator != null)
                {
                    animator.SetBool("Grounded", false);
                    animator.SetTrigger("Jump");
                }
            }
            else if (stopJump)
            {
                stopJump = false;
                if (velocity.y > 0)
                {
                    velocity.y = velocity.y * model.jumpDeceleration;
                }
            }

            UpdateAnimation();
            targetVelocity = move * maxSpeed;
        }

        void UpdateAnimation()
        {
            if (spriteRenderer != null)
            {
                if (move.x > 0.01f)
                    spriteRenderer.flipX = false;
                else if (move.x < -0.01f)
                    spriteRenderer.flipX = true;
            }

            if (animator != null)
            {
                animator.SetBool("Grounded", IsGrounded && velocity.y <= 0.01f);
                animator.SetFloat("AirSpeedY", velocity.y);

                if (IsGrounded)
                    animator.SetInteger("AnimState", Mathf.Abs(move.x) > 0.01f ? 1 : 0);
            }
        }

        Vector2 GetAimDirection()
        {
            if (Mouse.current != null && Camera.main != null)
            {
                var world = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                var dir = (Vector2)(world - transform.position);
                if (dir.sqrMagnitude > 0.0001f)
                    return dir.normalized;
            }
            return spriteRenderer != null && spriteRenderer.flipX ? Vector2.left : Vector2.right;
        }

        void TryStartHook()
        {
            var origin = (Vector2)transform.position;
            var aim = GetAimDirection();

            RaycastHit2D best = new RaycastHit2D();
            bool found = false;
            var hits = Physics2D.RaycastAll(origin, aim, hookRange, hookableLayers);
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider == collider2d || h.collider.isTrigger)
                    continue;
                if (!found || h.distance < best.distance)
                {
                    best = h;
                    found = true;
                }
            }

            if (!found)
                return;

            grappling = true;
            grapplePoint = best.point;

            if (hookAudio != null && audioSource != null)
                audioSource.PlayOneShot(hookAudio);

            if (rope != null)
            {
                rope.enabled = true;
                UpdateRope();
            }
        }

        void StopHook()
        {
            if (!grappling)
                return;
            grappling = false;
            if (rope != null)
                rope.enabled = false;
        }

        void CreateRope()
        {
            var go = new GameObject("Hook Rope");
            go.transform.SetParent(transform, false);
            rope = go.AddComponent<LineRenderer>();
            rope.positionCount = 2;
            rope.startWidth = 0.05f;
            rope.endWidth = 0.05f;
            rope.useWorldSpace = true;
            rope.numCapVertices = 2;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
                rope.material = new Material(shader);
            var ropeColor = new Color(0.4f, 0.25f, 0.1f, 1f);
            rope.startColor = ropeColor;
            rope.endColor = ropeColor;
            rope.enabled = false;
        }

        void UpdateRope()
        {
            if (rope == null)
                return;
            rope.SetPosition(0, transform.position);
            rope.SetPosition(1, grapplePoint);
        }

        void LateUpdate()
        {
            if (grappling)
                UpdateRope();
        }
    }
}
