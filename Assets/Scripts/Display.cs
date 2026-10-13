
using TMPro;
using UnityEngine;

public class Display : MonoBehaviour
{
    Rigidbody rb;
    public TMP_Text velocityText;
    public TMP_Text accelerationText;
    public TMP_Text altitudalText;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float speed = rb.linearVelocity.magnitude;
        velocityText.text = $"Velocity: {speed.ToString("F2")} m/s";

        

        float dt = Time.fixedDeltaTime;
        Vector3 acc = (rb.linearVelocity - rb.linearVelocity) / dt;
        accelerationText.text = $"Acceleration: {acc.magnitude.ToString("F2")} m/s²";


        float altitude = rb.position.y;
        altitudalText.text = $"Altitude: {altitude.ToString("F2")} m";
    
    }
}
