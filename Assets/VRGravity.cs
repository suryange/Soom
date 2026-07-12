using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class VRGravity : MonoBehaviour
{
    private CharacterController cc;
    private float verticalVelocity = 0f;
    private float gravity = -9.81f;

    void Start()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (cc.isGrounded)
        {
            verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 move = new Vector3(0, verticalVelocity, 0);
        cc.Move(move * Time.deltaTime);
    }
}