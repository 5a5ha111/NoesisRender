using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircularMovementTest : MonoBehaviour
{
    public Vector3 centerPoint = Vector3.zero; // Center of the circular path
    public float radius = 5.0f;                // Radius of the circle
    public float angularSpeed = 1.0f;          // Angular speed in radians per second
    private float angle = 0.0f;                // Current angle

    void Update()
    {
        // Calculate the new position
        float x = centerPoint.x + radius * Mathf.Cos(angle);
        float z = centerPoint.z + radius * Mathf.Sin(angle);
        transform.position = new Vector3(x, transform.position.y, z);

        // Update the angle based on the angular speed and time elapsed
        angle += angularSpeed * Time.deltaTime;

        // Keep the angle within the range 0 to 2*PI for numerical stability
        if (angle >= 2 * Mathf.PI)
        {
            angle -= 2 * Mathf.PI;
        }
    }
}
