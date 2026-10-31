
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
    private float avoidanceSideSign;
    private float avoidanceClearTimer;
    private bool warnedLowThrust;
    public bool IsAvoidingObstacle { get; private set; }


    [Header("Replanning")]
    [Min(0.01f)]
    public float targetMoveThreshold = 1f;

    private int observedPathVersion = -1;
    private Vector3 plannedTargetPosition;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        smoothedTargetAngle = transform.eulerAngles.z;

        if (propulsion == null)
        propulsion = GetComponent<Propulsion>();

        if(navigation == null)
        navigation = GetComponent<Navigation>();
        

        

    }


    
    // Update is called once per frame
    void FixedUpdate()
    {
        if (propulsion == null) return;
        if (hasLanded || !propulsion.autopilotEnabled || rb.isKinematic)
        {
            propulsion.ApplyThrust(0f);
            return;
        }
        if (navigation == null || navigation.landingTarget == null)
        {
            propulsion.ApplyThrust(0f);
            return;
        }

        UpdateNavigation();
        if (navigation.Path == null || navigation.Path.Count == 0)
        {
            currentTargetSpeed = 0f;
            initialized = false;
            propulsion.ApplyThrust(0f);
            return;
        }
        AdvanceReachedWaypoints();
        waypointIndex = Mathf.Clamp(waypointIndex, 0, navigation.Path.Count - 1);
        Vector3 target = navigation.Path[waypointIndex];
        // A smoothed position can cut across the inside of an obstacle corner.
        // Follow the checked waypoint directly; smooth only the tilt command.
        smoothedTarget = target;
        initialized = true;
        Vector3 offset = target - transform.position;
        offset.z = 0f;
        Vector3 velocity = rb.linearVelocity;
        velocity.z = 0f;
        float dt = Time.fixedDeltaTime;
        float gain = Mathf.Max(0.1f, accelerationStrength);
        Vector3 gravity = rb.useGravity ? Physics.gravity : Vector3.zero;
        gravity.z = 0f;
        float available = Mathf.Max(0f, propulsion.thrustForce) / Mathf.Max(0.0001f, rb.mass);
        if (!warnedLowThrust && available <= gravity.magnitude)
        {
            Debug.LogWarning("Insufficient thrust to climb and evade: increase Thrust Force above Rigidbody mass * gravity magnitude.", this);
            warnedLowThrust = true;
        }
        float brakeAcceleration = Mathf.Max(0.25f, Mathf.Min(gain, available * 0.35f));
        float speed = Mathf.Min(Mathf.Max(0f, targetSpeed), Mathf.Max(0f, maxSpeed),
            Mathf.Sqrt(2f * brakeAcceleration * offset.magnitude));
        currentTargetSpeed = Mathf.MoveTowards(currentTargetSpeed, speed, gain * dt);
        Vector3 desiredVelocity = offset.normalized * currentTargetSpeed;

        Vector3 landingPosition = navigation.landingTarget.position;
        float horizontalError = landingPosition.x - transform.position.x;
        float altitude = transform.position.y - landingPosition.y;
        bool approachingLanding = waypointIndex == navigation.Path.Count - 1
            && Mathf.Abs(horizontalError) <= Mathf.Max(0.1f, landingHorizontalTolerance)
            && altitude >= 0f && altitude <= Mathf.Max(0.5f, landingHeight * 2f)
            && navigation.HasLineOfSight(transform.position, target);

        if (approachingLanding)
        {
            desiredVelocity.x = Mathf.Clamp(horizontalError * 1.5f, -Mathf.Max(0.1f, landingSpeed), Mathf.Max(0.1f, landingSpeed));
            desiredVelocity.y = -Mathf.Min(Mathf.Max(0.05f, landingSpeed), Mathf.Max(0f, altitude));
        }

        float speedScale;
        Vector3 avoidanceAcceleration = AvoidObstacle(velocity, desiredVelocity, brakeAcceleration, out speedScale);
        // Slow before turning; local avoidance and navigation share one torque controller.
        desiredVelocity *= speedScale;
        Vector3 desiredAcceleration = (desiredVelocity - velocity) * gain - gravity + avoidanceAcceleration;
        desiredAcceleration.z = 0f;
        float tiltLimit = Mathf.Clamp(approachingLanding && !IsAvoidingObstacle ? 12f : maxTiltAngle, 1f, 80f);
        float targetAngle = Mathf.Clamp(Vector2.SignedAngle(Vector2.up,
            new Vector2(desiredAcceleration.x, Mathf.Max(0.01f, desiredAcceleration.y))), -tiltLimit, tiltLimit);
        float response = 1f - Mathf.Exp(-dt / Mathf.Max(0.02f, targetSmoothTime));
        smoothedTargetAngle = Mathf.LerpAngle(smoothedTargetAngle, targetAngle, response);
        float angleError = Mathf.DeltaAngle(transform.eulerAngles.z, smoothedTargetAngle) * Mathf.Deg2Rad;
        float steering = angleError * Mathf.Max(0f, steeringStrength)
            - rb.angularVelocity.z * Mathf.Max(0f, angularDamping);
        float torqueLimit = Mathf.Max(0f, maxSteeringTorque);
        rb.AddTorque(Vector3.forward * Mathf.Clamp(steering, -torqueLimit, torqueLimit), ForceMode.Acceleration);

        float requiredAcceleration = Mathf.Max(0f, Vector3.Dot(desiredAcceleration, transform.up));
        // Do not switch off all lift just because a steering angle exceeds 15 degrees.
        propulsion.ApplyThrust(available > 0.001f ? Mathf.Clamp01(requiredAcceleration / available) : 0f);
    }

    void UpdateNavigation()
    {
        if (observedPathVersion != navigation.PathVersion)
        AdoptCurrentPath();

        repathTimer += Time.fixedDeltaTime;

        if (repathTimer < Mathf.Max(0.1f, pathRefreshInterval))
        return;

        repathTimer = 0f;

        bool missingPath = navigation.Path == null || navigation.Path.Count == 0;

        float threshold = Mathf.Max(0.01f, targetMoveThreshold);

        bool targetMove = (navigation.landingTarget.position - plannedTargetPosition).sqrMagnitude >= threshold * threshold;

        if (!missingPath && !targetMove && !RemainingRouteBlocked())
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
        if (navigation.Path == null || navigation.Path.Count == 0)
            return true;

        int index = Mathf.Clamp(waypointIndex, 0, navigation.Path.Count - 1);
        if (!navigation.HasLineOfSight(transform.position, navigation.Path[index]))
            return true;

        for (int i = index; i < navigation.Path.Count - 1; i++)
        {
            if (!navigation.HasLineOfSight(navigation.Path[i], navigation.Path[i + 1]))
                return true;
        }
        return false;
    }

    void AdvanceReachedWaypoints()
    {
        if (navigation.Path == null || navigation.Path.Count == 0)
            return;

        waypointIndex = Mathf.Clamp(waypointIndex, 0, navigation.Path.Count - 1);
        float reachDistance = Mathf.Max(0.05f, waypointDistance);

        while (waypointIndex < navigation.Path.Count - 1)
        {
            Vector3 offset = navigation.Path[waypointIndex] - transform.position;
            offset.z = 0f;
            if (offset.sqrMagnitude > reachDistance * reachDistance)
                break;
            if (!navigation.HasLineOfSight(transform.position, navigation.Path[waypointIndex + 1]))
                break;

            waypointIndex++;
            smoothedTarget = navigation.Path[waypointIndex];
            targetSmoothVelocity = Vector3.zero;
        }
    }

    public void ResetFlight()
    {
        avoidanceSideSign = 0f;
        avoidanceClearTimer = 0f;
        warnedLowThrust = false;
        IsAvoidingObstacle = false;
        hasLanded = false;
        landingMode = false;
        initialized = false;
        waypointIndex = 0;
        observedPathVersion = -1;
        repathTimer = Mathf.Max(0.1f, pathRefreshInterval);
        currentTargetSpeed = 0f;
        smoothedTarget = transform.position;
        targetSmoothVelocity = Vector3.zero;
        smoothedTargetAngle = transform.eulerAngles.z;
        if (propulsion != null) propulsion.ApplyThrust(0f);
    }

    Vector3 AvoidObstacle(Vector3 velocity, Vector3 desiredVelocity,
        float brakeAcceleration, out float speedScale)
    {
        speedScale = 1f;
        float radius = Mathf.Max(0.05f, avoidanceDetectionRadius);
        float reactionTime = Mathf.Max(0.1f, targetSmoothTime) + 0.25f;
        float stoppingDistance = velocity.sqrMagnitude / (2f * Mathf.Max(0.25f, brakeAcceleration));
        float range = Mathf.Max(Mathf.Max(0.1f, avoidanceDetectionDistance),
            stoppingDistance + velocity.magnitude * reactionTime + radius);
        Vector3 travel = velocity.sqrMagnitude > 0.25f ? velocity.normalized : desiredVelocity.normalized;
        if (travel.sqrMagnitude < 0.001f) travel = transform.up;
        travel.z = 0f;
        travel = travel.normalized;
        Vector3 wanted = desiredVelocity.sqrMagnitude > 0.001f ? desiredVelocity.normalized : travel;
        RaycastHit hit;
        bool blocked = Physics.SphereCast(transform.position, radius, travel, out hit,
            range, obstacleMask, QueryTriggerInteraction.Ignore);
        RaycastHit wantedHit;
        if (Physics.SphereCast(transform.position, radius, wanted, out wantedHit,
            range, obstacleMask, QueryTriggerInteraction.Ignore)
            && (!blocked || wantedHit.distance < hit.distance))
        {
            hit = wantedHit;
            travel = wanted;
            blocked = true;
        }
        IsAvoidingObstacle = blocked;
        if (!blocked)
        {
            avoidanceClearTimer += Time.fixedDeltaTime;
            if (avoidanceClearTimer > 0.4f) avoidanceSideSign = 0f;
            return Vector3.zero;
        }
        avoidanceClearTimer = 0f;
        Vector3 side = new Vector3(travel.y, -travel.x, 0f).normalized;
        float sideRange = Mathf.Max(0.1f, avoidanceSideDistance);
        RaycastHit rightHit;
        RaycastHit leftHit;
        bool rightBlocked = Physics.SphereCast(transform.position, radius, side,
            out rightHit, sideRange, obstacleMask, QueryTriggerInteraction.Ignore);
        bool leftBlocked = Physics.SphereCast(transform.position, radius, -side,
            out leftHit, sideRange, obstacleMask, QueryTriggerInteraction.Ignore);
        float rightRoom = rightBlocked ? rightHit.distance : sideRange;
        float leftRoom = leftBlocked ? leftHit.distance : sideRange;
        if (avoidanceSideSign == 0f)
        {
            if (Mathf.Abs(rightRoom - leftRoom) > 0.1f)
                avoidanceSideSign = rightRoom > leftRoom ? 1f : -1f;
            else
                avoidanceSideSign = Vector3.Dot(side, Vector3.up) >= 0f ? 1f : -1f;
        }
        // Change sides only if the chosen escape is substantially more obstructed.
        if (avoidanceSideSign > 0f && rightRoom < radius && leftRoom > rightRoom + radius)
            avoidanceSideSign = -1f;
        else if (avoidanceSideSign < 0f && leftRoom < radius && rightRoom > leftRoom + radius)
            avoidanceSideSign = 1f;

        float safeSpeed = Mathf.Sqrt(2f * brakeAcceleration * Mathf.Max(0f, hit.distance - radius));
        speedScale = Mathf.Clamp01(safeSpeed / Mathf.Max(0.1f, Mathf.Max(velocity.magnitude, desiredVelocity.magnitude)));
        float urgency = 1f - Mathf.Clamp01(hit.distance / range);
        Vector3 normal = hit.normal;
        normal.z = 0f;
        Vector3 escape = side * avoidanceSideSign + normal.normalized * 0.5f;
        // Suppress lateral escape when both immediate sides are closed; prioritize braking.
        if (leftRoom < radius && rightRoom < radius) escape = normal.normalized;
        float strength = Mathf.Max(0f, avoidanceStrengthMultiplier) * Mathf.Max(0.1f, accelerationStrength);
        return escape.normalized * strength * Mathf.Max(0.2f, urgency)
            + Vector3.up * Mathf.Max(0f, avoidanceUpwardForce) * urgency;
    }
}
