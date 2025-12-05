using System.Collections;
using UnityEngine;

namespace nightmareBW
{
    public interface IDragonBossState
    {
        void Enter();
        void Tick();
        void Exit();
    }

    public abstract class DragonBossBaseState : IDragonBossState
    {
        protected DragonBossController controller;

        protected DragonBossBaseState(DragonBossController controller)
        {
            this.controller = controller;
        }

        public virtual void Enter()
        {
        }

        public virtual void Tick()
        {
            controller.CheckPhaseTransition();
        }

        public virtual void Exit()
        {
        }
    }

    public class DragonBossController : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public DragonRangedAttack rangedAttack;
        public Bar bossHealthBar;

        [Header("Animation")]
        [SerializeField] string moveSpeedParam = "Speed";

        [Header("Movement")]
        [SerializeField] public float speed = 5f;
        [SerializeField] float nodeReachDistance = 3f;
        [SerializeField] float pathRebuildInterval = 1f;
        [SerializeField] float rotationSpeed = 5f;

        [Header("Strafing")]
        public float strafeSpeedMultiplier = 0.6f;
        public float strafeDuration = 1.2f;
        public float strafeCooldown = 1.5f;
        public float preStrafeBackDuration = 1.5f;

        enum StrafeDirection
        {
            None,
            Left,
            Right,
            Back
        }

        StrafeDirection currentStrafeDirection = StrafeDirection.None;
        StrafeDirection queuedStrafeDirection = StrafeDirection.None;
        float strafeTimer;
        float strafeCooldownTimer;

        [Header("Health")]
        public float maxHealth = 100f;
        public float currentHealth = 100f;
        [Range(0f, 1f)] public float phase2Threshold = 0.66f;
        [Range(0f, 1f)] public float phase3Threshold = 0.33f;

        [Header("Distance Tuning")]
        public float stopDistanceFromPlayer = 4f;

        [Header("Ranges")]
        public float meleeRange = 3f;
        public float preferredRange = 8f;

        [Header("Damage / Sensors")]
        public LayerMask playerLayer;
        public Transform meleePoint;
        public float meleeRadius = 2f;
        public int meleeDamage = 1;
        public float meleeKnockback = 2f;
        public float basicMeleeHitDelay = 0.75f;
        public float clawMeleeHitDelay = 1.55f;

        public Transform aoePoint;
        public float aoeRadius = 4f;
        public int aoeDamage = 2;
        public float aoeKnockback = 3f;
        public float aoeHitDelay = 0.5f;

        [Header("Defensive / Punish")]
        public float punishDistance = 1.5f;
        public float punishCloseTime = 2f;
        public float defensiveDuration = 2f;
        public float heavyHitThreshold = 25f;

        // Pathfinding
        Navigator navigator;
        Transform _transform;
        Rigidbody _rigidbody;
        Transform player;

        Vector3 targetVelocity;
        Vector3 currentTargetNodePosition;
        int pathNodeIndex = 0;
        float pathRebuildTimer = 0f;
        float closeTimer = 0f;

        // States
        IDragonBossState currentState;
        IDragonBossState previousState;

        Phase1State phase1State;
        Phase2State phase2State;
        Phase3State phase3State;
        DefensiveState defensiveState;

        void Awake()
        {
            navigator = GetComponent<Navigator>();
            _rigidbody = GetComponent<Rigidbody>();
            _transform = transform;

            PlayerLogic p = FindObjectOfType<PlayerLogic>();
            if (p != null)
            {
                player = p.transform;
            }

            phase1State = new Phase1State(this);
            phase2State = new Phase2State(this);
            phase3State = new Phase3State(this);
            defensiveState = new DefensiveState(this);

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
            // Punish if player stays too close
            if (player != null)
            {
                float dist = GetDistanceToPlayer();
                if (dist < punishDistance)
                {
                    closeTimer += Time.deltaTime;
                    if (closeTimer >= punishCloseTime)
                    {
                        EnterDefensiveState();
                        closeTimer = 0f;
                    }
                }
                else
                {
                    closeTimer = 0f;
                }
            }

            if (currentState != null)
            {
                currentState.Tick();
            }

            UpdateStrafeTimers();
            UpdateAnimatorSpeed();
        }

        void UpdateAnimatorSpeed()
        {
            if (animator == null) return;

            Vector3 vel;

            if (_rigidbody != null)
            {
                vel = _rigidbody.linearVelocity;
            }
            else
            {
                vel = targetVelocity;
            }

            vel.y = 0f;
            float speedValue = vel.magnitude;

            if (speedValue < 0.05f)
            {
                speedValue = 0f;
            }

            animator.SetFloat(moveSpeedParam, speedValue);
        }

        void FixedUpdate()
        {
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = targetVelocity;
            }
        }

        // State machine core
        public void SwitchState(IDragonBossState newState, bool rememberPrevious = true)
        {
            if (newState == null) return;
            if (currentState == newState) return;

            if (currentState != null)
            {
                currentState.Exit();
            }

            if (rememberPrevious)
            {
                previousState = currentState;
            }

            currentState = newState;
            currentState.Enter();
        }

        public void ReturnToPreviousState()
        {
            if (previousState == null) return;
            SwitchState(previousState, false);
        }

        public void SetPhaseIndex(int index)
        {
            if (animator != null)
            {
                animator.SetInteger("PhaseIndex", index);
            }
        }

        public bool IsInAttackAnimation()
        {
            if (rangedAttack != null && rangedAttack.IsAttacking)
                return true;

            if (animator == null) return false;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            return state.IsTag("Attack");
        }

        // Phase helpers
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

        public void CheckPhaseTransition()
        {
            if (currentState == defensiveState)
                return;

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

        // Damage and death
        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;

            currentHealth -= amount;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

            if (bossHealthBar != null)
            {
                bossHealthBar.UpdateBar(0, (int)currentHealth);
            }

            if (amount >= heavyHitThreshold)
            {
                EnterDefensiveState();
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        void Die()
        {
            if (animator != null)
            {
                animator.SetTrigger("Die");
            }

            if (navigator != null)
            {
                navigator.enabled = false;
            }

            targetVelocity = Vector3.zero;

            if (GameManager.instance != null)
            {
                GameManager.instance.GoToNextLevel();
            }

            enabled = false;
        }

        public void EnterDefensiveState()
        {
            SwitchState(defensiveState);
        }

        public float GetDefensiveDuration()
        {
            return defensiveDuration;
        }

        public DragonRangedAttack GetRangedAttack()
        {
            return rangedAttack;
        }

        void DoMeleeHit()
        {
            if (meleePoint == null) return;

            Collider[] hits = Physics.OverlapSphere(meleePoint.position, meleeRadius, playerLayer);
            foreach (Collider hit in hits)
            {
                Damageable damageable = hit.GetComponentInParent<Damageable>();
                if (damageable == null) continue;

                Vector3 dir = hit.transform.position - transform.position;
                dir.Normalize();

                Damage dmg = new Damage();
                dmg.amount = meleeDamage;
                dmg.direction = dir;
                dmg.knockbackForce = meleeKnockback;

                damageable.Hit(dmg);

                break;
            }

        }

        void DoAoEHit()
        {
            if (aoePoint == null) return;

            Collider[] hits = Physics.OverlapSphere(aoePoint.position, aoeRadius, playerLayer);
            foreach (Collider hit in hits)
            {
                Damageable damageable = hit.GetComponentInParent<Damageable>();
                if (damageable == null) continue;

                Vector3 dir = hit.transform.position - transform.position;
                dir.Normalize();

                Damage dmg = new Damage();
                dmg.amount = aoeDamage;
                dmg.direction = dir;
                dmg.knockbackForce = aoeKnockback;

                damageable.Hit(dmg);

                break;
            }
        }

        public bool AttemptMakePathToPlayer()
        {
            if (navigator == null || player == null) return false;
            bool success = navigator.CalculatePathToPosition(player.position);
            if (success)
            {
                pathNodeIndex = 0;
                pathRebuildTimer = 0f;
            }
            return success;
        }

        public void UpdatePathMovement()
        {
            if (player == null) return;

            if (IsInAttackAnimation())
            {
                StopMovement();
                return;
            }

            if (IsStrafing())
            {
                Vector3 toPlayer = player.position - _transform.position;
                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude < 0.001f)
                {
                    StopMovement();
                    return;
                }

                toPlayer.Normalize();

                Vector3 moveDir = Vector3.zero;

                switch (currentStrafeDirection)
                {
                    case StrafeDirection.Left:
                        moveDir = Vector3.Cross(Vector3.up, toPlayer);
                        break;
                    case StrafeDirection.Right:
                        moveDir = Vector3.Cross(toPlayer, Vector3.up);
                        break;
                    case StrafeDirection.Back:
                        moveDir = -toPlayer;
                        break;
                }

                moveDir.Normalize();

                targetVelocity = moveDir * speed * strafeSpeedMultiplier;

                if (_rigidbody != null)
                {
                    targetVelocity.y = _rigidbody.linearVelocity.y;
                }

                return;
            }


            float distToPlayer = GetDistanceToPlayer();
            if (distToPlayer <= stopDistanceFromPlayer)
            {
                StopMovement();
                return;
            }

            if (navigator == null || navigator.PathNodes == null || navigator.PathNodes.Count == 0)
            {
                Vector3 dirToPlayer = player.position - _transform.position;
                dirToPlayer.y = 0f;
                dirToPlayer.Normalize();

                targetVelocity = dirToPlayer * speed;

                if (_rigidbody != null)
                {
                    targetVelocity.y = _rigidbody.linearVelocity.y;
                }

                return;
            }

            currentTargetNodePosition = navigator.PathNodes[pathNodeIndex];

            Vector3 dirToNode = currentTargetNodePosition - _transform.position;
            dirToNode.y = 0f;
            dirToNode.Normalize();

            float distToNode = Vector3.Distance(currentTargetNodePosition, _transform.position);

            if (distToNode < nodeReachDistance)
            {
                pathNodeIndex++;

                if (pathNodeIndex >= navigator.PathNodes.Count)
                {
                    pathNodeIndex = 0;
                    AttemptMakePathToPlayer();
                    return;
                }
            }

            targetVelocity = dirToNode * speed;

            if (_rigidbody != null)
            {
                targetVelocity.y = _rigidbody.linearVelocity.y;
            }

            pathRebuildTimer += Time.deltaTime;
            if (pathRebuildTimer >= pathRebuildInterval)
            {
                pathRebuildTimer = 0f;
                if (navigator.CalculatePathToPosition(player.position))
                {
                    if (navigator.PathNodes.Count > 1)
                    {
                        pathNodeIndex = 1;
                    }
                    else
                    {
                        pathNodeIndex = 0;
                    }
                }
            }
        }

        public void StopMovement()
        {
            if (_rigidbody != null)
            {
                targetVelocity = new Vector3(0f, _rigidbody.linearVelocity.y, 0f);
            }
            else
            {
                targetVelocity = Vector3.zero;
            }
        }

        public bool IsStrafing()
        {
            return currentStrafeDirection != StrafeDirection.None;
        }


        void StartRandomStrafe()
        {
            if (player == null) return;
            if (IsInAttackAnimation()) return;
            if (IsStrafing()) return;
            if (strafeCooldownTimer > 0f) return;

            float dist = GetDistanceToPlayer();

            StrafeDirection sideDir = (Random.value < 0.5f)
                ? StrafeDirection.Left
                : StrafeDirection.Right;

            if (dist <= stopDistanceFromPlayer)
            {
                currentStrafeDirection = StrafeDirection.Back;
                queuedStrafeDirection = sideDir;

                if (animator != null)
                {
                    animator.SetTrigger("StrafeBack");
                }

                strafeTimer = preStrafeBackDuration;
                strafeCooldownTimer = preStrafeBackDuration + strafeDuration + strafeCooldown;
            }
            else
            {
                currentStrafeDirection = sideDir;
                queuedStrafeDirection = StrafeDirection.None;

                if (animator != null)
                {
                    if (sideDir == StrafeDirection.Left)
                    {
                        animator.SetTrigger("StrafeLeft");
                    }
                    else
                    {
                        animator.SetTrigger("StrafeRight");
                    }
                }

                strafeTimer = strafeDuration;
                strafeCooldownTimer = strafeDuration + strafeCooldown;
            }
        }


        void UpdateStrafeTimers()
        {
            if (IsStrafing())
            {
                strafeTimer -= Time.deltaTime;

                if (strafeTimer <= 0f)
                {
                    if (currentStrafeDirection == StrafeDirection.Back &&
                        queuedStrafeDirection != StrafeDirection.None)
                    {
                        currentStrafeDirection = queuedStrafeDirection;
                        queuedStrafeDirection = StrafeDirection.None;

                        if (animator != null)
                        {
                            if (currentStrafeDirection == StrafeDirection.Left)
                            {
                                animator.SetTrigger("StrafeLeft");
                            }
                            else if (currentStrafeDirection == StrafeDirection.Right)
                            {
                                animator.SetTrigger("StrafeRight");
                            }
                        }

                        strafeTimer = strafeDuration;
                    }
                    else
                    {
                        currentStrafeDirection = StrafeDirection.None;
                        queuedStrafeDirection = StrafeDirection.None;
                    }
                }
            }

            if (strafeCooldownTimer > 0f)
            {
                strafeCooldownTimer -= Time.deltaTime;
            }
        }


        public float GetDistanceToPlayer()
        {
            if (player == null) return Mathf.Infinity;
            return Vector3.Distance(_transform.position, player.position);
        }

        public void FacePlayer()
        {
            if (player == null) return;

            if (IsInAttackAnimation())
                return;

            Vector3 dir = player.position - _transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir);
            _transform.rotation = Quaternion.Slerp(
                _transform.rotation,
                targetRot,
                Time.deltaTime * rotationSpeed
            );
        }

        public void PlayMeleeAttack()
        {
            if (animator == null) return;

            if (Random.value < 0.5f)
            {
                animator.SetTrigger("BasicAttack");
                StartCoroutine(MeleeHitRoutine(GetScaledDelay(basicMeleeHitDelay)));
            }
            else
            {
                animator.SetTrigger("ClawAttack");
                StartCoroutine(MeleeHitRoutine(GetScaledDelay(clawMeleeHitDelay)));
            }
        }


        IEnumerator MeleeHitRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (IsInAttackAnimation())
            {
                DoMeleeHit();
            }
        }


        public void PlayAoEAttack()
        {
            if (animator != null)
            {
                animator.SetTrigger("AoEAttack");
            }

            StartCoroutine(AoEHitRoutine(GetScaledDelay(aoeHitDelay)));
        }

        IEnumerator AoEHitRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            DoAoEHit();
        }

        public void PlayDefensiveMove()
        {
            if (animator != null)
            {
                animator.SetTrigger("Defend");
            }
        }

        public float GetScaledDelay(float baseDelay)
        {
            float referenceSpeed = 3f;
            return baseDelay * (referenceSpeed / speed);
        }



        // Phase 1: basic melee focus
        public class Phase1State : DragonBossBaseState
        {
            float attackTimer;

            const float meleeCooldown = 2.0f;

            public Phase1State(DragonBossController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                controller.speed = 3f;
                controller.SetPhaseIndex(1);
                controller.AttemptMakePathToPlayer();

            }

            public override void Tick()
            {
                base.Tick();

                float dist = controller.GetDistanceToPlayer();

                if (controller.IsStrafing())
                {
                    controller.UpdatePathMovement();
                }
                else if (dist > controller.stopDistanceFromPlayer)
                {
                    controller.UpdatePathMovement();
                }
                else
                {
                    controller.StopMovement();

                    if (Random.value < 0.25f)
                    {
                        controller.StartRandomStrafe();
                    }
                }

                controller.FacePlayer();

                if (controller.IsInAttackAnimation())
                    return;

                float meleeZone = controller.meleeRange;

                attackTimer -= Time.deltaTime;

                if (attackTimer <= 0f && dist <= meleeZone)
                {
                    controller.StopMovement();
                    controller.PlayMeleeAttack();
                    attackTimer = meleeCooldown;
                }
            }


        }

        // Phase 2: faster, mixes melee and AoE
        public class Phase2State : DragonBossBaseState
        {
            float attackTimer;

            const float meleeCooldown = 1.5f;
            const float aoeCooldown = 3.0f;


            public Phase2State(DragonBossController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                controller.speed = 4f;
                controller.SetPhaseIndex(2);
                controller.AttemptMakePathToPlayer();
            }

            public override void Tick()
            {
                base.Tick();

                float dist = controller.GetDistanceToPlayer();

                if (controller.IsStrafing())
                {
                    controller.UpdatePathMovement();
                }
                else if (dist > controller.stopDistanceFromPlayer)
                {
                    controller.UpdatePathMovement();
                }
                else
                {
                    controller.StopMovement();

                    if (Random.value < 0.35f)
                    {
                        controller.StartRandomStrafe();
                    }
                }

                controller.FacePlayer();

                if (controller.IsInAttackAnimation())
                    return;

                attackTimer -= Time.deltaTime;
                if (attackTimer > 0f) return;

                float meleeZone = controller.meleeRange;
                float aoeZoneStart = controller.meleeRange + 1.5f;
                float aoeZoneEnd = controller.preferredRange;

                if (dist <= meleeZone)
                {
                    float roll = Random.value;

                    if (roll < 0.7f)
                    {
                        controller.StopMovement();
                        controller.PlayMeleeAttack();
                        attackTimer = meleeCooldown;
                    }
                    else
                    {
                        controller.StopMovement();
                        controller.PlayAoEAttack();
                        attackTimer = aoeCooldown;
                    }
                }
                else if (dist >= aoeZoneStart && dist <= aoeZoneEnd)
                {
                    controller.StopMovement();
                    controller.PlayAoEAttack();
                    attackTimer = aoeCooldown;
                }
                else
                {
                    attackTimer = 0.25f;
                }
            }

        }


        // Phase 3: enraged, uses melee up close and ranged when far enough away
        public class Phase3State : DragonBossBaseState
        {
            float attackTimer;

            const float meleeCooldown = 1.0f;
            const float aoeCooldown = 2.5f;
            const float thinkDelay = 0.25f;

            public Phase3State(DragonBossController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;

                controller.speed = 6f;
                controller.SetPhaseIndex(3);
                controller.AttemptMakePathToPlayer();
            }

            public override void Tick()
            {
                base.Tick();

                float dist = controller.GetDistanceToPlayer();

                if (controller.IsStrafing())
                {
                    controller.UpdatePathMovement();
                }
                else if (dist > controller.stopDistanceFromPlayer)
                {
                    controller.UpdatePathMovement();
                }
                else
                {
                    controller.StopMovement();

                    if (Random.value < 0.5f)
                    {
                        controller.StartRandomStrafe();
                    }
                }

                controller.FacePlayer();

                if (controller.IsInAttackAnimation())
                    return;

                attackTimer -= Time.deltaTime;
                if (attackTimer > 0f) return;

                float meleeZone = controller.meleeRange;
                float rangedZoneMin = controller.meleeRange + 3f;
                float rangedZoneMax = controller.preferredRange;

                DragonRangedAttack ranged = controller.GetRangedAttack();
                bool canRanged = (ranged != null && ranged.CanUse());

                if (dist <= meleeZone)
                {
                    float roll = Random.value;

                    if (roll < 0.5f)
                    {
                        controller.StopMovement();
                        controller.PlayMeleeAttack();
                        attackTimer = meleeCooldown;
                    }
                    else
                    {
                        controller.StopMovement();
                        controller.PlayAoEAttack();
                        attackTimer = aoeCooldown;
                    }
                }
                else if (dist >= rangedZoneMin && dist <= rangedZoneMax && canRanged)
                {
                    controller.StopMovement();
                    controller.FacePlayer();
                    ranged.StartRangedAttack();
                    attackTimer = thinkDelay;
                }
                else if (dist >= rangedZoneMin && dist <= rangedZoneMax)
                {
                    if (Random.value < 0.4f)
                    {
                        controller.StopMovement();
                        controller.PlayAoEAttack();
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

        // Defensive state: punish/guard when player stays close or deals heavy damage
        public class DefensiveState : DragonBossBaseState
        {
            float timer;

            public DefensiveState(DragonBossController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                base.Enter();
                timer = controller.GetDefensiveDuration();
                controller.StopMovement();
                controller.PlayDefensiveMove();
            }

            public override void Tick()
            {
                timer -= Time.deltaTime;
                controller.FacePlayer();

                if (timer <= 0f)
                {
                    controller.ReturnToPreviousState();
                }
            }
        }
    }
}

