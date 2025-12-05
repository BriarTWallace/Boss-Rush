using UnityEngine;

namespace nightmareBW
{
    public class DragonBossMovement : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public string moveSpeedParam = "Speed";

        [Header("Player and Pathfinding")]
        public float stopDistanceFromPlayer = 5f;
        public float preferredRange = 10f;
        public float nodeReachDistance = 3f;
        public float pathRebuildInterval = 1f;
        public float rotationSpeed = 5f;

        [Header("Base Movement Speed")]
        public float speed = 3f;

        [Header("Evasive Movement")]
        public float strafeSpeedMultiplier = 0.6f;
        public float backoffSpeedMultiplier = 0.6f;
        public float backoffDuration = 0.7f;
        public float strafeDuration = 1f;
        public float strafeCooldown = 1.5f;

        [Header("Evasive Behaviour Tuning")]
        public bool autoEvasive = true;
        public float evasiveChance = 0.3f;

        enum EvasiveMode
        {
            None,
            BackOnly,
            StrafeOnly,
            BackThenStrafe
        }

        enum StrafeDirection
        {
            None,
            Left,
            Right
        }

        EvasiveMode currentEvasiveMode = EvasiveMode.None;
        StrafeDirection currentStrafeDirection = StrafeDirection.None;
        StrafeDirection queuedStrafeDirection = StrafeDirection.None;

        float backoffTimer;
        float strafeTimer;
        float evasiveCooldownTimer;

        Navigator navigator;
        Transform player;
        Transform _transform;
        Rigidbody _rigidbody;

        Vector3 targetVelocity;
        Vector3 currentTargetNodePosition;
        int pathNodeIndex;
        float pathRebuildTimer;

        bool movementLocked;

        void Awake()
        {
            _transform = transform;
            navigator = GetComponent<Navigator>();
            _rigidbody = GetComponent<Rigidbody>();

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            PlayerLogic p = FindObjectOfType<PlayerLogic>();
            if (p != null)
            {
                player = p.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    player = playerObj.transform;
                }
            }
        }

        void Update()
        {
            UpdateEvasiveTimers();
            UpdateMovementLogic();
            UpdateAnimatorSpeed();
            FacePlayer();
        }

        void FixedUpdate()
        {
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = targetVelocity;
            }
        }

        public void LockMovement(bool locked)
        {
            movementLocked = locked;
            if (locked)
            {
                StopMovement();
            }
        }

        public void SetMoveSpeed(float newSpeed)
        {
            speed = newSpeed;
        }

        public void SetStopDistance(float newStopDistance)
        {
            stopDistanceFromPlayer = newStopDistance;
        }

        public float GetDistanceToPlayer()
        {
            if (player == null) return Mathf.Infinity;
            return Vector3.Distance(_transform.position, player.position);
        }

        public bool IsStrafing()
        {
            return currentEvasiveMode != EvasiveMode.None &&
                   currentStrafeDirection != StrafeDirection.None;
        }

        public bool IsBackingOff()
        {
            return currentEvasiveMode == EvasiveMode.BackOnly ||
                   currentEvasiveMode == EvasiveMode.BackThenStrafe;
        }

        void UpdateMovementLogic()
        {
            if (movementLocked || player == null)
            {
                StopMovement();
                return;
            }

            if (IsBackingOff() || IsStrafing())
            {
                UpdatePathMovement();
                return;
            }

            float dist = GetDistanceToPlayer();

            if (dist > stopDistanceFromPlayer)
            {
                UpdatePathMovement();
            }
            else
            {
                StopMovement();

                if (autoEvasive)
                {
                    float meleeRange = stopDistanceFromPlayer;
                    float maxStrafeDist = preferredRange;

                    if (dist > meleeRange && dist < maxStrafeDist)
                    {
                        if (Random.value < evasiveChance)
                        {
                            StartRandomEvasive();
                        }
                    }
                    else if (dist <= meleeRange)
                    {
                        if (Random.value < evasiveChance * 0.5f)
                        {
                            StartRandomEvasive();
                        }
                    }
                }
            }
        }

        public void UpdatePathMovement()
        {
            if (player == null) return;

            Vector3 toPlayer = player.position - _transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude < 0.001f)
            {
                StopMovement();
                return;
            }

            toPlayer.Normalize();

            if (IsBackingOff())
            {
                Vector3 moveDir = -toPlayer;

                targetVelocity = moveDir * speed * backoffSpeedMultiplier;

                if (_rigidbody != null)
                {
                    targetVelocity.y = _rigidbody.linearVelocity.y;
                }

                return;
            }

            if (IsStrafing())
            {
                Vector3 moveDir;

                if (currentStrafeDirection == StrafeDirection.Left)
                {
                    moveDir = Vector3.Cross(Vector3.up, toPlayer);
                }
                else
                {
                    moveDir = Vector3.Cross(toPlayer, Vector3.up);
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

        void FacePlayer()
        {
            if (player == null) return;
            if (movementLocked) return;

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

        void StartRandomEvasive()
        {
            if (player == null) return;
            if (IsBackingOff() || IsStrafing()) return;
            if (evasiveCooldownTimer > 0f) return;

            float dist = GetDistanceToPlayer();
            float roll = Random.value;

            StrafeDirection sideDir = (Random.value < 0.5f)
                ? StrafeDirection.Left
                : StrafeDirection.Right;

            if (dist <= stopDistanceFromPlayer + 0.5f)
            {
                if (roll < 0.5f)
                {
                    BeginBackoff(EvasiveMode.BackOnly, StrafeDirection.None);
                }
                else
                {
                    BeginBackoff(EvasiveMode.BackThenStrafe, sideDir);
                }
            }
            else
            {
                if (roll < 0.4f)
                {
                    BeginStrafe(sideDir);
                }
                else
                {
                    BeginBackoff(EvasiveMode.BackThenStrafe, sideDir);
                }
            }

            evasiveCooldownTimer = backoffDuration + strafeDuration + strafeCooldown;
        }

        void BeginBackoff(EvasiveMode mode, StrafeDirection sideDir)
        {
            currentEvasiveMode = mode;
            queuedStrafeDirection = sideDir;
            backoffTimer = backoffDuration;

            if (animator != null)
            {
                animator.SetTrigger("StrafeBack");
            }
        }

        void BeginStrafe(StrafeDirection dir)
        {
            currentEvasiveMode = EvasiveMode.StrafeOnly;
            currentStrafeDirection = dir;
            strafeTimer = strafeDuration;

            if (animator != null)
            {
                if (dir == StrafeDirection.Left)
                {
                    animator.SetTrigger("StrafeLeft");
                }
                else if (dir == StrafeDirection.Right)
                {
                    animator.SetTrigger("StrafeRight");
                }
            }
        }

        void EndEvasive()
        {
            currentEvasiveMode = EvasiveMode.None;
            currentStrafeDirection = StrafeDirection.None;
            queuedStrafeDirection = StrafeDirection.None;
            backoffTimer = 0f;
            strafeTimer = 0f;
        }

        void UpdateEvasiveTimers()
        {
            if (IsBackingOff())
            {
                backoffTimer -= Time.deltaTime;

                if (backoffTimer <= 0f)
                {
                    if (currentEvasiveMode == EvasiveMode.BackThenStrafe &&
                        queuedStrafeDirection != StrafeDirection.None)
                    {
                        BeginStrafe(queuedStrafeDirection);
                        queuedStrafeDirection = StrafeDirection.None;
                    }
                    else
                    {
                        EndEvasive();
                    }
                }
            }
            else if (IsStrafing())
            {
                strafeTimer -= Time.deltaTime;

                if (strafeTimer <= 0f)
                {
                    EndEvasive();
                }
            }

            if (evasiveCooldownTimer > 0f)
            {
                evasiveCooldownTimer -= Time.deltaTime;
            }
        }
    }
}
