using Platformer.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Ataque cuerpo a cuerpo del jugador (click izquierdo).
    /// El golpe alcanza a los enemigos que estén dentro del rango, les quita
    /// vida y dispara la animación de ataque, el sonido y el efecto visual.
    /// Si un enemigo se queda sin vida, el evento HealthIsZero se encarga de
    /// matarlo.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerMeleeAttack : MonoBehaviour
    {
        [Header("Alcance")]
        [Tooltip("Alcance del ataque cuerpo a cuerpo (click izquierdo).")]
        [SerializeField] private float meleeRange = 1.2f;

        [Header("Daño")]
        [Tooltip("Daño que quita cada golpe.")]
        [SerializeField] private int meleeDamage = 1;
        [Tooltip("Segundos que hay que esperar entre golpes.")]
        [SerializeField] private float attackCooldown = 0.25f;

        [Header("Efectos")]
        [Tooltip("Sonido reproducido al golpear.")]
        [SerializeField] private AudioClip attackAudio;
        [Tooltip("Color del efecto visual del golpe.")]
        [SerializeField] private Color attackColor = new Color(1f, 0.95f, 0.75f, 1f);

        PlayerController player;
        SpriteRenderer spriteRenderer;
        Animator animator;
        InputAction attackAction;

        int comboStep;
        float lastAttackTime;
        float nextAttackTime;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
        }

        void OnEnable()
        {
            attackAction = InputSystem.actions.FindAction("Player/Attack");
            if (attackAction != null)
                attackAction.Enable();
        }

        void Update()
        {
            if (player == null || !player.ControlEnabled)
                return;

            if (AttackPressed())
                MeleeAttack();
        }

        bool AttackPressed()
        {
            if (attackAction != null)
                return attackAction.WasPressedThisFrame();

            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        /// <summary>
        /// Ejecuta un golpe cuerpo a cuerpo hacia donde mira el jugador.
        /// </summary>
        public void MeleeAttack()
        {
            if (Time.time < nextAttackTime)
                return;

            if (Time.time - lastAttackTime > 1f)
                comboStep = 0;

            lastAttackTime = Time.time;
            nextAttackTime = Time.time + attackCooldown;
            comboStep = comboStep % 3 + 1;

            if (animator != null)
                animator.SetTrigger("Attack" + comboStep);

            Vector2 facing = spriteRenderer != null && spriteRenderer.flipX ? Vector2.left : Vector2.right;
            Vector2 center = (Vector2)transform.position + facing * (meleeRange * 0.5f);

            DamageEnemies(center, meleeRange);
            PlayAttackSound();
            CircleEffect.Spawn(center, meleeRange, attackColor);
        }

        void DamageEnemies(Vector2 center, float radius)
        {
            var hits = Physics2D.OverlapCircleAll(center, radius);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<EnemyController>();
                if (enemy == null)
                    continue;

                var health = enemy.GetComponent<Health>();
                if (health == null || !health.IsAlive)
                    continue;

                for (int i = 0; i < meleeDamage && health.IsAlive; i++)
                    health.Decrement();

                if (health.IsAlive)
                {
                    var enemyAnimator = enemy.GetComponent<Animator>();
                    if (enemyAnimator != null)
                        enemyAnimator.SetTrigger("hurt");
                }
            }
        }

        void PlayAttackSound()
        {
            if (attackAudio != null && player != null && player.AudioSource != null)
                player.AudioSource.PlayOneShot(attackAudio);
        }
    }
}
