using UnityEngine;

namespace nightmareBW
{
    public class DragonFireball : MonoBehaviour
    {
        [Header("Projectile Settings")]
        public float speed = 15f;
        public float damage = 20f;
        public float lifeTime = 5f;

        [Header("Explosion Settings")]
        public float explosionRadius = 0f;

        Vector3 moveDirection;
        bool initialized;

        public void Init(Vector3 direction)
        {
            moveDirection = direction.normalized;
            initialized = true;
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            if (!initialized) return;

            transform.position += moveDirection * speed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Boss"))
                return;

            if (explosionRadius > 0f)
            {
                DoExplosion();
            }
            else
            {
                TryDamage(other);
            }

            Destroy(gameObject);
        }

        void DoExplosion()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (Collider hit in hits)
            {
                TryDamage(hit);
            }
        }

        void TryDamage(Collider col)
        {
            /*var health = col.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }*/
        }
    }
}

