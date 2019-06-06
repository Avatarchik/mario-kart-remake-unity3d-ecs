using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    // the higher this value is, the faster it will lock on to the target, the lower this value is, the more time it will smooth
    public float smoothSpeed = 0.5f;
    public Vector3 offset;

    // Update is called once per frame
    void LateUpdate()
    {
        if(target != null)
        {
            Vector3 newPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Slerp(transform.position, newPosition, smoothSpeed * Time.deltaTime);
            transform.position = newPosition;

            transform.LookAt(target);
        }
        else
        {
            
        }
    }
}
