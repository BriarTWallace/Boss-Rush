using System.Collections;
using UnityEngine;

namespace nightmareBW
{
    // Interface for class-based state machine
    public interface IDragonBossState
    {
        void Enter();
        void Tick();
        void Exit();
    }

    // Base state that handles phase transitions
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

        [Header("Attack Movement Lock")]
        public float attackMoveLockDuration = 0.8f;
        bool isAttackLocked;
        float attackLockTimer;

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

            if (isAttackLocked)
            {
                attackLockTimer -= Time.deltaTime;
                if (attackLockTimer <= 0f)
                {
                    isAttackLocked = false;
                }
            }

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

        // Animator phase helper
        public void SetPhaseIndex(int index)
        {
            if (animator != null)
            {
                animator.SetInteger("PhaseIndex", index);
            }
        }

        public void LockMovementForAttack()
        {
            isAttackLocked = true;
            attackLockTimer = attackMoveLockDuration;
            StopMovement();
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

            if (isAttackLocked)
            {
                StopMovement();
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

                if (dirToPlayer.sqrMagnitude > 0.001f)
                {
                    _transform.forward = dirToPlayer;
                }

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

            if (dirToNode.sqrMagnitude > 0.001f)
            {
                _transform.forward = dirToNode;
            }

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

            targetVelocity = _transform.forward * speed;

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

        public float GetDistanceToPlayer()
        {
            if (player == null) return Mathf.Infinity;
            return Vector3.Distance(_transform.position, player.position);
        }

        public void FacePlayer()
        {
            if (player == null) return;

            Vector3 dir = player.position - _transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir);
            _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRot, Time.deltaTime * 10f);
        }

        public void PlayMeleeAttack()
        {
            if (animator == null) return;

            if (Random.value < 0.5f)
            {
                animator.SetTrigger("BasicAttack");
            }
            else
            {
                animator.SetTrigger("ClawAttack");
            }
        }

        public void PlayAoEAttack()
        {
            if (animator != null)
            {
                animator.SetTrigger("AoEAttack");
            }
        }

        public void PlayDefensiveMove()
        {
            if (animator != null)
            {
                animator.SetTrigger("Defend");
            }
        }


        // Phase 1: basic melee focus
        public class Phase1State : DragonBossBaseState
        {
            float attackTimer;

            public Phase1State(DragonBossController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                controller.speed = 4f;
                controller.SetPhaseIndex(1);
                controller.AttemptMakePathToPlayer();

            }

            public override void Tick()
            {
                base.Tick();

                float dist = controller.GetDistanceToPlayer();

                if (dist > controller.meleeRange)
                {
                    controller.UpdatePathMovement();
                }
                else
                {
                    controller.StopMovement();
                }

                controller.FacePlayer();

                attackTimer -= Time.deltaTime;

                if (attackTimer <= 0f && dist <= controller.meleeRange)
                {
                    controller.StopMovement();
                    controller.LockMovementForAttack();
                    controller.PlayMeleeAttack();
                    attackTimer = 2f;
                }
            }
        }

        // Phase 2: faster, mixes melee and AoE
        public class Phase2State : DragonBossBaseState
        {
            float attackTimer;

            public Phase2State(DragonBossController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                controller.speed = 5f;
                controller.SetPhaseIndex(2);
                controller.AttemptMakePathToPlayer();
            }

            public override void Tick()
            {
                base.Tick();

                float dist = controller.GetDistanceToPlayer();

                if (dist > controller.stopDistanceFromPlayer)
                {
                    controller.UpdatePathMovement();
                }
                else
                {
                    controller.StopMovement();
                }

                controller.FacePlayer();

                attackTimer -= Time.deltaTime;

                if (attackTimer <= 0f)
                {
                    if (dist <= controller.meleeRange)
                    {
                        controller.StopMovement();
                        controller.LockMovementForAttack();
                        controller.PlayMeleeAttack();
                    }
                    else
                    {
                        controller.StopMovement();
                        controller.LockMovementForAttack();
                        controller.PlayAoEAttack();
                    }

                    attackTimer = 1.5f;
                }
            }
        }


        // Phase 3: enraged, uses ranged attack plus others
        public class Phase3State : DragonBossBaseState
        {
            float attackTimer;

            public Phase3State(DragonBossController controller) : base(controller)
            {
            }

            public override void Enter()
            {
                base.Enter();
                attackTimer = 0f;
                controller.speed = 7f;
                controller.SetPhaseIndex(3);
                controller.AttemptMakePathToPlayer();
            }

            public override void Tick()
            {
                base.Tick();

                float dist = controller.GetDistanceToPlayer();

                if (dist > controller.stopDistanceFromPlayer)
                {
                    controller.UpdatePathMovement();
                }
                else
                {
                    controller.StopMovement();
                }

                controller.FacePlayer();

                attackTimer -= Time.deltaTime;

                if (attackTimer <= 0f)
                {
                    if (dist <= controller.meleeRange)
                    {
                        controller.StopMovement();
                        controller.LockMovementForAttack();
                        controller.PlayMeleeAttack();
                    }
                    else
                    {
                        DragonRangedAttack ranged = controller.GetRangedAttack();
                        if (ranged != null && ranged.CanUse())
                        {
                            controller.StopMovement();
                            controller.LockMovementForAttack();
                            controller.FacePlayer();
                            ranged.StartRangedAttack();
                        }
                        else
                        {
                            controller.StopMovement();
                            controller.LockMovementForAttack();
                            controller.PlayAoEAttack();
                        }
                    }

                    attackTimer = 1.2f;
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

