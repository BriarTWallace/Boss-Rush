using System.Collections;
using UnityEngine;

namespace nightmareBW
{
    public class DragonBossAttack : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public DragonBossMovement movement;
        public DragonRangedAttack rangedAttack;

        [Header("Melee Settings")]
        public Transform meleePoint;
        public float meleeRadius = 2f;
        public int meleeDamage = 1;
        public float meleeKnockback = 2f;
        public float basicMeleeHitDelay = 0.8f;
        public float clawMeleeHitDelay = 1.65f;
        public float meleePostAttackLock = 0.2f;
        public float meleeCooldown = 2f;

        [Header("AoE Settings")]
        public Transform aoePoint;
        public float aoeRadius = 4f;
        public int aoeDamage = 2;
        public float aoeKnockback = 3f;
        public float aoeHitDelay = 1.2f;
        public float aoePostAttackLock = 0.25f;
        public float aoeCooldown = 3f;

        [Header("Ranged Settings")]
        public float rangedPostAttackLock = 0.2f;
        public float rangedCooldown = 2.5f;

        [Header("Per-Phase Timing Multipliers")]
        public float phase1DelayMultiplier = 1f;
        public float phase2DelayMultiplier = 1.2f;
        public float phase3DelayMultiplier = 1.4f;

        float currentDelayMultiplier = 1f;

        float nextMeleeTime;
        float nextAoETime;
        float nextRangedTime;

        public bool IsAttacking { get; private set; }



        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (movement == null)
                movement = GetComponent<DragonBossMovement>();

            if (rangedAttack == null)
                rangedAttack = GetComponent<DragonRangedAttack>();
        }

        public void SetPhase(int phaseIndex)
        {
            if (phaseIndex == 1)
            {
                currentDelayMultiplier = phase1DelayMultiplier;
            }
            else if (phaseIndex == 2)
            {
                currentDelayMultiplier = phase2DelayMultiplier;
            }
            else if (phaseIndex == 3)
            {
                currentDelayMultiplier = phase3DelayMultiplier;
            }
            else
            {
                currentDelayMultiplier = 1f;
            }
        }
        float GetPhaseDelayMultiplier()
        {
            return currentDelayMultiplier;
        }

        public bool CanMelee()
        {
            return Time.time >= nextMeleeTime && !IsAttacking;
        }

        public bool CanAoE()
        {
            return Time.time >= nextAoETime && !IsAttacking;
        }

        public bool CanRanged()
        {
            if (IsAttacking) return false;
            if (rangedAttack == null) return false;
            if (!rangedAttack.CanUse()) return false;
            return Time.time >= nextRangedTime;
        }

        public void TryMeleeAttack()
        {
            if (!CanMelee()) return;
            StartCoroutine(MeleeRoutine());
        }

        public void TryAoEAttack()
        {
            if (!CanAoE()) return;
            StartCoroutine(AoERoutine());
        }

        public void TryRangedAttack()
        {
            if (!CanRanged()) return;
            StartCoroutine(RangedRoutine());
        }

        IEnumerator MeleeRoutine()
        {
            IsAttacking = true;
            if (movement != null) movement.LockMovement(true);

            bool useBasic = Random.value < 0.5f;

            if (animator != null)
            {
                if (useBasic)
                    animator.SetTrigger("BasicAttack");
                else
                    animator.SetTrigger("ClawAttack");
            }

            float baseDelay = useBasic ? basicMeleeHitDelay : clawMeleeHitDelay;
            float delay = baseDelay * GetPhaseDelayMultiplier();

            yield return new WaitForSeconds(delay);
            DoMeleeHit();

            yield return new WaitForSeconds(meleePostAttackLock);

            if (movement != null) movement.LockMovement(false);
            IsAttacking = false;
            nextMeleeTime = Time.time + meleeCooldown;
        }

        IEnumerator AoERoutine()
        {
            IsAttacking = true;
            if (movement != null) movement.LockMovement(true);

            if (animator != null)
            {
                animator.SetTrigger("AoEAttack");
            }

            float delay = aoeHitDelay * GetPhaseDelayMultiplier();

            yield return new WaitForSeconds(aoeHitDelay);
            DoAoEHit();

            yield return new WaitForSeconds(aoePostAttackLock);

            if (movement != null) movement.LockMovement(false);
            IsAttacking = false;
            nextAoETime = Time.time + aoeCooldown;
        }

        IEnumerator RangedRoutine()
        {
            IsAttacking = true;
            if (movement != null) movement.LockMovement(true);

            if (rangedAttack != null)
            {
                rangedAttack.StartRangedAttack();
            }

            if (rangedAttack != null)
            {
                while (rangedAttack.IsAttacking)
                    yield return null;
            }
            else
            {
                yield return new WaitForSeconds(0.8f);
            }

            yield return new WaitForSeconds(rangedPostAttackLock);

            if (movement != null) movement.LockMovement(false);
            IsAttacking = false;
            nextRangedTime = Time.time + rangedCooldown;
        }

        void DoMeleeHit()
        {
            if (meleePoint == null) return;

            Collider[] hits = Physics.OverlapSphere(meleePoint.position, meleeRadius);
            foreach (Collider hit in hits)
            {
                if (!hit.CompareTag("Player"))
                    continue;

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

            Collider[] hits = Physics.OverlapSphere(aoePoint.position, aoeRadius);
            foreach (Collider hit in hits)
            {
                if (!hit.CompareTag("Player"))
                    continue;

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

        void OnDrawGizmosSelected()
        {
            if (meleePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(meleePoint.position, meleeRadius);
            }

            if (aoePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(aoePoint.position, aoeRadius);
            }
        }
    }
}
