using System.Collections;
using UnityEngine;

namespace nightmareBW
{
    public class DragonRangedAttack : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public Transform firePoint;
        public GameObject fireballPrefab;

        [Header("Ranged Settings")]
        public float cooldown = 4f;
        public float castRange = 20f;

        public float spawnDelay = 0.6f;
        public float endDelay = 0.6f;

        Transform player;
        bool isOnCooldown;
        bool isAttacking;

        void Awake()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
            }
            else
            {
                PlayerLogic pl = FindObjectOfType<PlayerLogic>();
                if (pl != null)
                {
                    player = pl.transform;
                }
            }
        }

        public bool CanUse()
        {
            if (isOnCooldown || isAttacking) return false;
            if (player == null) return false;

            float distance = Vector3.Distance(transform.position, player.position);
            return distance <= castRange;
        }

        public void StartRangedAttack()
        {
            if (!CanUse()) return;

            isAttacking = true;
            isOnCooldown = true;

            if (animator != null)
            {
                animator.SetTrigger("RangedAttack");
            }

            StartCoroutine(RangedRoutine());
        }

        IEnumerator RangedRoutine()
        {

            yield return new WaitForSeconds(spawnDelay);

            SpawnFireball();

            yield return new WaitForSeconds(endDelay);

            isAttacking = false;

            StartCoroutine(CooldownRoutine());
        }

        IEnumerator CooldownRoutine()
        {
            yield return new WaitForSeconds(cooldown);
            isOnCooldown = false;
        }

        void SpawnFireball()
        {
            if (fireballPrefab == null || firePoint == null || player == null)
                return;

            Vector3 target = player.position + Vector3.up * 1.2f;
            Vector3 direction = (target - firePoint.position).normalized;

            GameObject obj = Instantiate(
                fireballPrefab,
                firePoint.position,
                Quaternion.LookRotation(direction)
            );

            DragonFireball fb = obj.GetComponent<DragonFireball>();
            if (fb != null)
            {
                fb.Init(direction);
            }
        }
    }
}

