using UnityEngine;

namespace nightmareBW
{
    public interface IDragonState
    {
        void Enter();
        void Tick();
        void Exit();
    }

    public abstract class DragonStateBase : IDragonState
    {
        protected DragonBossBrain brain;
        protected DragonBossMovement movement;
        protected DragonBossAttack attack;

        protected DragonStateBase(DragonBossBrain brain)
        {
            this.brain = brain;
            this.movement = brain.movement;
            this.attack = brain.attack;
        }

        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    public class DragonBossBrain : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public DragonBossMovement movement;
        public DragonBossAttack attack;
        public DragonRangedAttack rangedAttack;
        public Bar bossHealthBar;

        [Header("Health")]
        public float maxHealth = 100f;
        public float currentHealth = 100f;
        [Range(0f, 1f)] public float phase2Threshold = 0.66f;
        [Range(0f, 1f)] public float phase3Threshold = 0.33f;

        [Header("Ranges")]
        public float meleeRange = 5f;

        [Header("Phase Speeds")]
        public float phase1MoveSpeed = 3f;
        public float phase2MoveSpeed = 4f;
        public float phase3MoveSpeed = 6f;

        [Header("Punish / Defensive (optional)")]
        public bool enablePunish = true;
        public float punishDistance = 1.5f;
        public float punishCloseTime = 2f;

        float closeTimer;

        IDragonState currentState;
        IDragonState phase1State;
        IDragonState phase2State;
        IDragonState phase3State;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (movement == null) movement = GetComponent<DragonBossMovement>();
            if (attack == null) attack = GetComponent<DragonBossAttack>();
            if (rangedAttack == null) rangedAttack = GetComponent<DragonRangedAttack>();

            phase1State = new Phase1State(this);
            phase2State = new Phase2State(this);
            phase3State = new Phase3State(this);

            currentHealth = maxHealth;

            if (bossHealthBar != null)
            {
                bossHealthBar.SetMax((int)maxHealth);
                bossHealthBar.UpdateBar(0, (int)currentHealth);
            }
        }

        void Start()
        {
            SwitchState(phase1State);
        }

        void Update()
        {
            if (currentState != null)
            {
                currentState.Tick();
            }

            CheckPhaseTransition();
            UpdatePunishTimer();
        }

        void UpdatePunishTimer()
        {
            if (!enablePunish) return;
            if (movement == null) return;

            float dist = movement.GetDistanceToPlayer();
            if (dist < punishDistance)
            {
                closeTimer += Time.deltaTime;
                if (closeTimer >= punishCloseTime)
                {
                    if (attack != null)
                    {
                        attack.TryAoEAttack();
                    }
                    closeTimer = 0f;
                }
            }
            else
            {
                closeTimer = 0f;
            }
        }

        public float GetHealthPercent()
        {
            if (maxHealth <= 0f) return 0f;
            return currentHealth / maxHealth;
        }

        public bool IsInPhase1()
        {
            float hp = GetHealthPercent();
            return hp > phase2Threshold;
        }

        public bool IsInPhase2()
        {
            float hp = GetHealthPercent();
            return hp <= phase2Threshold && hp > phase3Threshold;
        }

        public bool IsInPhase3()
        {
            float hp = GetHealthPercent();
            return hp <= phase3Threshold;
        }

        void CheckPhaseTransition()
        {
            if (currentState == null) return;

            if (IsInPhase3() && currentState != phase3State)
            {
                SwitchState(phase3State);
            }
            else if (IsInPhase2() && currentState != phase2State)
            {
                SwitchState(phase2State);
            }
            else if (IsInPhase1() && currentState != phase1State)
            {
                SwitchState(phase1State);
            }
        }

        void SwitchState(IDragonState newState)
        {
            if (newState == null) return;
            if (currentState == newState) return;

            if (currentState != null)
            {
                currentState.Exit();
            }

            currentState = newState;
            currentState.Enter();
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;

            currentHealth -= amount;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

            if (bossHealthBar != null)
            {
                bossHealthBar.UpdateBar(0, (int)currentHealth);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }
        public void HandleHit(Damage dmg)
        {
            Debug.Log($"Boss hit for {dmg.amount}");
            TakeDamage(dmg.amount);
        }

        void Die()
        {
            if (animator != null)
            {
                animator.SetTrigger("Die");
            }

            if (movement != null)
            {
                movement.LockMovement(true);
                movement.enabled = false;
            }

            if (attack != null)
            {
                attack.enabled = false;
            }

            enabled = false;
        }

        void SetPhaseIndex(int index)
        {
            if (animator != null)
            {
                animator.SetInteger("PhaseIndex", index);
            }

            if (attack != null)
            {
                attack.SetPhase(index);
            }
        }

        // Phase 1: only melee, slowest
        class Phase1State : DragonStateBase
        {
            float attackTimer;
            const float meleeCooldown = 2f;

            public Phase1State(DragonBossBrain brain) : base(brain) { }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                if (movement != null)
                {
                    movement.SetMoveSpeed(brain.phase1MoveSpeed);
                }
                brain.SetPhaseIndex(1);
                if (movement != null)
                {
                    movement.AttemptMakePathToPlayer();
                }
            }

            public override void Tick()
            {
                if (movement == null || attack == null) return;

                float dist = movement.GetDistanceToPlayer();


                if (attack.IsAttacking) return;

                attackTimer -= Time.deltaTime;
                if (attackTimer > 0f) return;

                if (dist <= brain.meleeRange && attack.CanMelee())
                {
                    attack.TryMeleeAttack();
                    attackTimer = meleeCooldown;
                }
            }
        }

        // Phase 2: melee plus AoE, a bit faster
        class Phase2State : DragonStateBase
        {
            float attackTimer;
            const float meleeCooldown = 1.6f;
            const float aoeCooldown = 3f;

            public Phase2State(DragonBossBrain brain) : base(brain) { }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                if (movement != null)
                {
                    movement.SetMoveSpeed(brain.phase2MoveSpeed);
                }
                brain.SetPhaseIndex(2);
                if (movement != null)
                {
                    movement.AttemptMakePathToPlayer();
                }
            }

            public override void Tick()
            {
                if (movement == null || attack == null) return;

                float dist = movement.GetDistanceToPlayer();

                if (attack.IsAttacking) return;

                attackTimer -= Time.deltaTime;
                if (attackTimer > 0f) return;

                float meleeZone = brain.meleeRange;
                float aoeZoneStart = brain.meleeRange + 1.5f;
                float aoeZoneEnd = movement.preferredRange;

                if (dist <= meleeZone && attack.CanMelee())
                {
                    float roll = Random.value;
                    if (roll < 0.7f)
                    {
                        attack.TryMeleeAttack();
                        attackTimer = meleeCooldown;
                    }
                    else if (attack.CanAoE())
                    {
                        attack.TryAoEAttack();
                        attackTimer = aoeCooldown;
                    }
                    else
                    {
                        attack.TryMeleeAttack();
                        attackTimer = meleeCooldown;
                    }
                }
                else if (dist >= aoeZoneStart && dist <= aoeZoneEnd && attack.CanAoE())
                {
                    attack.TryAoEAttack();
                    attackTimer = aoeCooldown;
                }
                else
                {
                    attackTimer = 0.25f;
                }
            }
        }

        // Phase 3: melee, AoE, ranged, fastest 
        class Phase3State : DragonStateBase
        {
            float attackTimer;
            const float meleeCooldown = 1.2f;
            const float aoeCooldown = 2.5f;
            const float thinkDelay = 0.25f;

            public Phase3State(DragonBossBrain brain) : base(brain) { }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                if (movement != null)
                {
                    movement.SetMoveSpeed(brain.phase3MoveSpeed);
                }
                brain.SetPhaseIndex(3);
                if (movement != null)
                {
                    movement.AttemptMakePathToPlayer();
                }
            }

            public override void Tick()
            {
                if (movement == null || attack == null) return;

                float dist = movement.GetDistanceToPlayer();

                if (attack.IsAttacking) return;

                attackTimer -= Time.deltaTime;
                if (attackTimer > 0f) return;

                float meleeZone = brain.meleeRange;
                float rangedZoneMin = brain.meleeRange + 3f;
                float rangedZoneMax = movement.preferredRange;

                bool canRanged = attack.CanRanged();

                if (dist <= meleeZone && attack.CanMelee())
                {
                    float roll = Random.value;

                    if (roll < 0.5f)
                    {
                        attack.TryMeleeAttack();
                        attackTimer = meleeCooldown;
                    }
                    else if (attack.CanAoE())
                    {
                        attack.TryAoEAttack();
                        attackTimer = aoeCooldown;
                    }
                    else
                    {
                        attack.TryMeleeAttack();
                        attackTimer = meleeCooldown;
                    }
                }
                else if (dist >= rangedZoneMin && dist <= rangedZoneMax && canRanged)
                {
                    attack.TryRangedAttack();
                    attackTimer = thinkDelay;
                }
                else if (dist >= rangedZoneMin && dist <= rangedZoneMax && attack.CanAoE())
                {
                    if (Random.value < 0.4f)
                    {
                        attack.TryAoEAttack();
                        attackTimer = aoeCooldown;
                    }
                    else
                    {
                        attackTimer = thinkDelay;
                    }
                }
                else
                {
                    attackTimer = thinkDelay;
                }
            }
        }
    }
}

