using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]

public class Autonomy : MonoBehaviour
{

    public Navigation navigation;
    public Propulsion propulsion;

    [Header("Navigation")]

    public float waypointDistance = 3f;
    public float pathRefreshInterval = 0.5f;
    public float targetSmoothTime = 0.6f;
    public float steeringStrength = 4f;
    public float maxSteeringTorque = 10f;

    
    public float maxTiltAngle = 55f;
    public float angularDamping = 2f;
    public float landingHorizontalTolerance = 1.5f;



    

    public float targetSpeed = 8f;
    public float brakingDistance = 10f;
    public float maxSpeed = 12f;

    public float accelerationStrength = 5f;
    public float landingSpeed = 1f;
    public float landingHeight = 2f;
    public float landingTolerance = 1f;
    public float thrustAngleTolerance = 15f;



    private Rigidbody rb;
    private int waypointIndex;

    private float repathTimer;
    private Vector3 smoothedTarget;

    public float smoothedTargetAngle;


    private Vector3 targetSmoothVelocity;

    public bool initialized;
    private bool landingMode;
    private bool hasLanded;

    private float currentTargetSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (propulsion == null)
        propulsion = GetComponent<Propulsion>();

        if(navigation == null)
        navigation = GetComponent<Navigation>();
        

        

    }


    
    // Update is called once per frame
    void FixedUpdate()
    {
       if (hasLanded)
       return;

      
       if (navigation == null || propulsion == null || navigation.landingTarget == null) return;
       
       if (navigation.Path == null || navigation.Path.Count == 0)
        {
            propulsion.ApplyThrust(0f);
            return;
        }
       repathTimer += Time.fixedDeltaTime;

       if (repathTimer >= pathRefreshInterval)
        {
            repathTimer = 0f;

            Vector3 previousTarget = smoothedTarget;

            navigation.RecalculatePath();

            if(navigation.Path != null && navigation.Path.Count > 0)
            {
                float closestDistance = float.MaxValue;
                int closestIndex = 0;

                for (int i = 0; i < navigation.Path.Count; i++)
                {
                    Vector3 offset = navigation.Path[i] - transform.position;
                    offset.z = 0f;
                
                    float breakingDistance = offset.sqrMagnitude;
                
                    if(brakingDistance < closestDistance)
                        {
                            closestDistance = brakingDistance;
                            closestIndex = i;
                        }
                }
                waypointIndex = closestIndex;

                smoothedTarget = previousTarget;
            }
        }
       
       if(navigation == null || propulsion == null || navigation.landingTarget == null || navigation.Path == null || navigation.Path.Count == 0) return;

        Vector3 landingPosition = navigation.landingTarget.position;

        float horizontalDistance = Mathf.Abs(transform.position.x - landingPosition.x);

        float altitude = transform.position.y - landingPosition.y;

        bool approachingLanding = horizontalDistance < brakingDistance * 3f && altitude >= 0f && altitude < brakingDistance * 3f;

        waypointIndex = Mathf.Clamp(waypointIndex, 0, navigation.Path.Count - 1);
        
        Vector3 target = navigation.Path[waypointIndex];
        
        if (!initialized)
        {
            smoothedTarget = target;
            initialized = true;


            smoothedTargetAngle = transform.eulerAngles.z;
        }   
    
        smoothedTarget = Vector3.SmoothDamp(smoothedTarget, target, ref targetSmoothVelocity, targetSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
        
        Vector3 direction = smoothedTarget - transform.position;
        direction.z = 0f;

        while (direction.magnitude < waypointDistance && waypointIndex < navigation.Path.Count - 1)
        {
            waypointIndex++;
            target = navigation.Path[waypointIndex];
            direction = target - transform.position;
            direction.z = 0f;

        }

       
       Vector2 desiredDirection;
       
       if(approachingLanding)
        {
            desiredDirection = Vector2.up;
        }
        else
        {
            desiredDirection = new Vector2(direction.x, direction.y).normalized;

            
        }

        float targetAngle = Vector2.SignedAngle(Vector2.up, desiredDirection);
        
        targetAngle = Mathf.Clamp(targetAngle, -maxTiltAngle, maxTiltAngle);

        float angleDifference = Mathf.DeltaAngle(smoothedTargetAngle, targetAngle);

        smoothedTargetAngle += angleDifference * Mathf.Clamp01(4f * Time.fixedDeltaTime);

        targetAngle = Mathf.Clamp(targetAngle, -maxTiltAngle, maxTiltAngle);

        desiredDirection = Quaternion.Euler(0f, 0f, targetAngle) * Vector2.up;

        float currentAngle = Mathf.DeltaAngle(0f, transform.eulerAngles.z);

        float angle = Mathf.DeltaAngle(currentAngle, smoothedTargetAngle);
        
       float steering = angle * Mathf.Deg2Rad * steeringStrength - rb.angularVelocity.z * angularDamping;

        rb.AddTorque(Vector3.forward * Mathf.Clamp(steering, -maxSteeringTorque, maxSteeringTorque), ForceMode.Acceleration);
    
        float distance = direction.magnitude;

        float desiredSpeed = Mathf.Min(targetSpeed, maxSpeed, Mathf.Sqrt(2f * accelerationStrength * Mathf.Max(0f, distance)));
        
        currentTargetSpeed = Mathf.MoveTowards(currentTargetSpeed, desiredSpeed, accelerationStrength * Time.fixedDeltaTime);
   
        float alignment = Vector2.Angle(new Vector2(transform.up.x, transform.up.y), desiredDirection);

        Vector3 desiredVelocity = direction.normalized * currentTargetSpeed;
        Vector3 velocityErrorNormal = desiredVelocity - rb.linearVelocity;

        



        Vector3 desiredAcceleration = velocityErrorNormal * accelerationStrength - Physics.gravity;

        float requiredAcceleration = Vector3.Dot(desiredAcceleration, transform.up);

        float maximumAcceleration = propulsion.thrustForce / rb.mass;
        
        float throttle = maximumAcceleration > 0.001f ? Mathf.Clamp01(requiredAcceleration / maximumAcceleration) : 0f;

        if (alignment < thrustAngleTolerance)
        {
            propulsion.ApplyThrust(throttle);
        }
    
    
    }
}
