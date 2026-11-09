using UnityEngine;

//Floor respawner.
//Place on the child object, the parent object is an empty transform that is the respawn point. 
// The child object should have a collider and rigidbody. When the child object collides with the floor, it will respawn after a timeout.
public class FloorContactRespawn : MonoBehaviour
{
    public string floorTag = "Floor"; // Tag for the floor object
    public float timeoutDuration = 3f; // Duration before respawn
    public Transform respawnPoint; // Reference to the respawn point
    private float respawnTimer = 0f; // Timer to track respawn
    private bool isTouchingFloor = false; // Flag to track if the object is touching the floor
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (respawnPoint == null)
        {
            //Default to parent transform if no respawn point is assigned
            respawnPoint = transform.parent;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void FixedUpdate()
    {
        // Check if the respawn timer is active
        if (respawnTimer > 0f)
        {
            // Decrease the timer based on the time elapsed since the last frame
            respawnTimer -= Time.fixedDeltaTime;

            // Check if the timer has reached zero or below
            if (respawnTimer <= 0f && isTouchingFloor)
            {
                // Respawn the object at the respawn point's position and rotation
                transform.position = respawnPoint.position;
                transform.rotation = respawnPoint.rotation;

                // Reset the object's velocity to zero to prevent it from moving after respawn
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }

    // Called when the collider enters a collision with another collider
    private void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object has the specified floor tag
        if (collision.gameObject.CompareTag(floorTag))
        {
            // Start the respawn timer
            respawnTimer = timeoutDuration; 
            isTouchingFloor = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        // Check if the collided object has the specified floor tag
        if (collision.gameObject.CompareTag(floorTag))
        {
            // Reset the respawn timer when leaving the floor
            respawnTimer = 0f;
            isTouchingFloor = false;
        }
    }
}
