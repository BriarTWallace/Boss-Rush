using UnityEngine;

namespace nightmareBW
{
    public class DragonBossHitReceiver : MonoBehaviour
    {
        public DragonBossBrain brain;
        public int damagePerHit = 1;
        public float hitCooldown = 0.15f;

        float lastHitTime;

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < lastHitTime + hitCooldown)
                return;

            var damager = other.GetComponent<Damager>();
            if (damager == null)
                return;

            if (brain != null)
            {
                brain.TakeDamage(damagePerHit);
                lastHitTime = Time.time;
            }
        }
    }
}

