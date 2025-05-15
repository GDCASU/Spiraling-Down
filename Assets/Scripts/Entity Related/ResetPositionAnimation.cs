using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetPositionAnimation : MonoBehaviour
{
    public GameObject[] listOfTimelineObjects;
    public Vector3[] newPositions;

    public void ResetPosition()
    {
        for (int i=0; i < listOfTimelineObjects.Length; i++)
        {
            listOfTimelineObjects[i].transform.localPosition = newPositions[i];
        }
    }
}
