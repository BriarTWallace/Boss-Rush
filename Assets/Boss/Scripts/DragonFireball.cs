using UnityEngine;

namespace nightmareBW
{
    public class DragonFireball : MonoBehaviour
    {
        [Header("Projectile Settings")]
        public float speed = 15f;
        public int damageAmount = 3;
        public float knockbackForce = 2f;
        public float lifeTime = 5f;

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

            Damageable damageable = other.GetComponent<Damageable>();
            if (damageable != null)
            {
                Vector3 dir = other.transform.position - transform.position;
                dir.Normalize();

                Damage damage = new Damage();
                damage.amount = damageAmount;
                damage.direction = dir;
                damage.knockbackForce = knockbackForce;

                damageable.Hit(damage);
            }

            Destroy(gameObject);
        }
    }
}


