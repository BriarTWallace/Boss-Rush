using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Actor : MonoBehaviour
{
    Rigidbody rigidBody;

    // Start is called before the first frame update
    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
    }

    public void ApplyKnockback(Damage damage)
    {
        if (rigidBody == null)
            return;

        StartCoroutine(HandleKnockback(damage.direction * damage.knockbackForce));
    }

    IEnumerator HandleKnockback(Vector3 knockback)
    {
        rigidBody.AddForce(knockback * 10, ForceMode.Impulse);
        yield return new WaitForSeconds(0.5f);
        rigidBody.linearVelocity = Vector3.zero;
    }
}
