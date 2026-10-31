
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]

public class Autonomy : MonoBehaviour
{


    [Header("Collision Avoidance")]
    public LayerMask obstacleMask = 1 << 6;
    public float avoidanceDetectionDistance = 6f;
    public float avoidanceDetectionRadius = 1f;
    public float avoidanceSideDistance = 3f;
    
    public float avoidanceStrengthMultiplier = 2f;

    public float avoidanceUpwardForce = 2f;
    
    
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


    [Header("Replanning")]
    [Min(0.01f)]
    public float targetMoveThreshold = 1f;

    private int observedPathVersion = -1;
    private Vector3 plannedTargetPosition;
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

       if (navigation == null || propulsion == null || navigation.landingTarget == null)
        {
            if (propulsion != null)
            propulsion.ApplyThrust(0f);

            return;
        }

        UpdateNavigation();

        if (navigation.Path.Count == 0)
        {
            currentTargetSpeed = 0f;
            initialized = false;
            propulsion.ApplyThrust(0f);
            return;

        }
        
        AdvanceReachedWaypoints();

        
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
        else
        {
            propulsion.ApplyThrust(0f);
        }
    
    
    }

    void UpdateNavigation()
    {
        if (observedPathVersion != navigation.PathVersion)
        AdoptCurrentPath();

        repathTimer += time.fixedDeltaTime;

        if (repathTimer < Mathf.Max(0.1f, pathRefreshInterval))
        return;

        repathTimer = 0f;

        bool missingPath = navigation.Path.Count == 0;

        float threshold = Mathf.Max(0.01f, targetMoveThreshold);

        bool targetMove = (navigation.landingTarget.position - plannedTargetPosition).sqrMagnitude >= threshold * threshold;

        if (!missingPath && !targetMoveThreshold && !RemainingRouteBlocked())
        return;

        navigation.RecalculatePath();
        AdoptCurrentPath();
    }

    void AdoptCurrentPath()
    {
        observedPathVersion = navigation.PathVersion;
        plannedTargetPosition = navigation.landingTarget.position;

        waypointIndex = 0;

        targetSmoothVelocity = Vector3.zero;
        initialized = false;

    }

    bool RemainingRouteBlocked()
    {
        int index = Mathf.Clamp(
            waypointIndex, 0, navigation.Path.Count - 1);

            // Connection from rocket towards the endpoint check

            
            if (!navigation.HasLineOfSight(
                transform.position, navigation.Path[index]))
            {
                return true;
            }

            for (int i = index; i < navigation.Path.Count - 1; i++)
        {
            if (!navigation.HasLineOfSight(
                navigation.Path[i], navigation.Path[i + 1]))
            {
                return true;
            }
        }

        void AdvanceReachedWaypoints()
        {
            Clamp(waypointIndex, 0, navigation.Path.Count - 1);

            float reachDistance = Mathf.Max(0.05f, waypointDistance);

            while (waypointIndex < navigation.Path.Count - 1)
            {
                Vector3 offset = navigation.Path[waypointIndex] - transform.position;

                offset.Z = 0f;

                if (offset.sqrMagnitude > reachDistance * reachDistance)
                break;

                //Only if connection is clear may it advance further through the function
                if (!navigation.HasLineOfSight(transform.position, navigation.Path[waypointIndex + 1]))
                {
                    break;
                }
                
                waypointIndex++;
                
                smoothedTarget = navigation.Path[waypointIndex];
                targetSmoothVelocity = Vector3.zero;
                
            }

        }
    

    
    }


    bool AvoidObstacle()
    {
        Vector3 forward = transform.up;
        Vector3 right = transform.right;

        if (!Physics.SphereCast(transform.position, avoidanceDetectionRadius, forward, out RaycastHit hit, avoidanceDetectionDistance, obstacleMask))
        {

            return false;
        }
        
        bool leftBlocked = Physics.SphereCast(transform.position, avoidanceDetectionRadius, -right, out RaycastHit leftHit, avoidanceSideDistance, obstacleMask);

        bool rightBlocked = Physics.SphereCast(transform.position, avoidanceDetectionRadius, right, out RaycastHit rightHit, avoidanceSideDistance, obstacleMask);
    
        float direction;
            
        if(leftBlocked && !rightBlocked)
        
        direction = 1f;

        else if (rightBlocked && !leftBlocked)
        
        direction = -1f;
        
        else

        direction = Vector3.Dot(hit.normal, right)  > 0f ? -1f : 1f;
        
        float proximity = 1f - hit.distance / avoidanceDetectionDistance;
        float avoidanceStrength = Mathf.SmoothStep(0.25f, 1f, proximity);
        Vector3 avoidanceDirection = (right * direction + forward * 0.5f + Vector3.up * avoidanceUpwardForce).normalized;

        Vector3 desiredDirection = (transform.up + avoidanceDirection * avoidanceStrength * 2f).normalized;

        float correctionAngle = 
        Vector2.SignedAngle(transform.up, desiredDirection);

        float steering = correctionAngle * Mathf.Deg2Rad * steeringStrength * avoidanceStrengthMultiplier - rb.angularVelocity.z * angularDamping;
        
        rb.AddTorque(Vector3.forward * Mathf.Clamp(steering, -maxSteeringTorque, maxSteeringTorque), ForceMode.Acceleration);
    
        return true;
    }    


}

