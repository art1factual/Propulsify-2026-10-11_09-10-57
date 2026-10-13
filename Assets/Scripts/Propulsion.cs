using System.Collections.Generic;
using System.Collections;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class Propulsion : MonoBehaviour
{

    [Header("Thrust")]
    public float thrustForce = 5f;
    public float rotationSpeed = 180f;
    public GameObject Thrust;
    public Vector2 thrustDirection;
    public Vector3 thrustDir;
    float rotationInput;

   [Header("Autopilot")]
    Rigidbody rb;
    public bool autopilotEnabled = true;
    public Transform landingTarget;

    public float correctionStrength = 8f;
    public float angularDamping = 3f;
    public float maxCorrectionTorque = 15f;
    

    void StabilizeUpright()
    {
        Quaternion uprightCorrection = Quaternion.FromToRotation(
            transform.up, Vector3.up);

            uprightCorrection.ToAngleAxis(out float angleDegrees, out Vector3 correctionAxis);

            if (angleDegrees > 180f)
            angleDegrees -= 360f;

            Vector3 correctionError = correctionAxis * angleDegrees * Mathf.Deg2Rad;

            Vector3 tiltingAngularVelocity = Vector3.ProjectOnPlane(rb.angularVelocity, Vector3.up);

            Vector3 torque = correctionError * correctionStrength - tiltingAngularVelocity * angularDamping;

            torque = Vector3.ClampMagnitude(torque, maxCorrectionTorque);

            rb.AddTorque(torque, ForceMode.Acceleration);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        
        if (autopilotEnabled)
        
           return;
        thrustDirection.y = Input.GetAxisRaw("Horizontal");
        thrustDirection.x = Input.GetAxisRaw("Vertical");
        
        thrustDir = transform.up; //transform.TransformVector(new Vector3(-thrustDirection.x, 1f, -thrustDirection.y).normalized);

        if (Input.GetKey(KeyCode.Space))
        {
            rb.AddForce(transform.up * thrustForce, ForceMode.Force);
        }

        if (Thrust != null)
        {
            Thrust.transform.localRotation = Quaternion.Euler(thrustDirection.y, 0f, thrustDirection.x);
        }

        
        
    }

    public void ApplyThrust(float throttle)
    {
        throttle = Mathf.Clamp01(throttle);

        rb.AddForce(transform.up * thrustForce * throttle, ForceMode.Force);
    }

}
