using UnityEngine;
using System.Collections;

/// <summary>
/// Basic orbit camera for Unity
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    /// <summary>
    /// Target transform
    /// </summary>
    public Transform target;

    /// <summary>
    /// Distance to the target
    /// </summary>
    public float distance = 5.0f;

    /// <summary>
    /// Horizontal orbit speed
    /// </summary>
    public float xSpeed = 120.0f;

    /// <summary>
    /// Vertical orbit speed
    /// </summary>
    public float ySpeed = 120.0f;

    /// <summary>
    /// Vertical lower angle limit
    /// </summary>
    public float yMinLimit = -20f;

    /// <summary>
    /// Vertical upper angle limit
    /// </summary>
    public float yMaxLimit = 80f;

    /// <summary>
    /// Minimal distance to target
    /// </summary>
    public float distanceMin = .5f;

    /// <summary>
    /// Maximal distance to target
    /// </summary>
    public float distanceMax = 15f;

    /// <summary>
    /// Interpolation factor (speed) for rotation
    /// </summary>
    public float slerpFactor = 0.1f;

    /// <summary>
    /// Interpolation factor (speed) for position
    /// </summary>
    public float lerpFactor = 0.1f;

    /// <summary>
    /// Rigid body of camera (to freeze rotation on it)
    /// </summary>
    private Rigidbody rb;

    /// <summary>
    /// Temporary value for horizontal input movement
    /// </summary>
    float x = 0.0f;

    /// <summary>
    /// Temporary value for vertical input movement
    /// </summary>
    float y = 0.0f;

    /// <summary>
    /// Initialization
    /// </summary>
    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        rb = GetComponent<Rigidbody>();

        // Don't rotate based on physics (if using rigid body)
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    /// <summary>
    /// Always update camera last
    /// </summary>
    void LateUpdate()
    {
        if (target)
        {
            // Take input (only when button pressed)
            if (Input.GetMouseButton(1))
            {
                x += Input.GetAxis("Mouse X") * xSpeed * distance * 0.02f;
                y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
            }

            // Limit angles
            y = ClampAngle(y, yMinLimit, yMaxLimit);

            // Handle distance (use scroll to zoom in and out)
            distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 2, distanceMin, distanceMax);

            // Perform a raycast between camera and target - in case there is an obstacle - place camera in front of it
            /*RaycastHit hit;
            if (Physics.Linecast(target.position, transform.position, out hit))
            {
                distance -= hit.distance;
            }*/

            // Calculate rotation delta - and set camera position to rotate around target position
            Quaternion rotation = Quaternion.Euler(y, x, 0);
            Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
            Vector3 position = rotation * negDistance + target.position;

            // Interpolate (to reduce noise/jumping of the camera)
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, slerpFactor);
            transform.position = Vector3.Lerp(transform.position, position, lerpFactor);
        }
    }

    /// <summary>
    /// Clamp angle between minimum and maximum
    /// </summary>
    /// <param name="angle">Input angle to clamp</param>
    /// <param name="min">Lower limit</param>
    /// <param name="max">Upper limit</param>
    /// <returns>Angle limited between min and max</returns>
    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
