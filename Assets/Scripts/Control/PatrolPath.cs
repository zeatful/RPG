namespace RPG.Control
{
    using UnityEngine;

    public class PatrolPath : MonoBehaviour
    {
        const float wayPointGizmoRadius = 0.3f;

        // called by unity
        private void OnDrawGizmos()
        {
            // iterate through each waypoint and add a sphere and draw lines between each to complete a path
            for (int i = 0; i < transform.childCount; i++)
            {
                Gizmos.DrawSphere(GetWaypoint(i), wayPointGizmoRadius);
                Gizmos.DrawLine(GetWaypoint(i), GetNextWayPoint(i));
            }
        }

        public Vector3 GetWaypoint(int i)
        {
            return transform.GetChild(i).position;
        }

        public Vector3 GetNextWayPoint(int i)
        {
            // ensure we grab proper next way point
            return GetWaypoint(GetNexWayPointIndex(i));
        }

        public int GetNexWayPointIndex(int i)
        {
            // ensure modular cycle of waypoints of patrol path
            return (i + 1 < transform.childCount) ? i + 1 : 0;
        }

        // used to initiate the guard to the nearest placed waypoint of the patrol path
        public int GetClosestWayPointIndex(Vector3 location)
        {
            // track the closest index and distance
            int closestWayPointIndex = 0;
            float closetDistance = Mathf.Infinity;

            // iterate through all waypoints and get their distance, storing the lowest distance and index
            for (int i = 0; i < transform.childCount; i++)
            {
                float distance = Vector3.Distance(location, transform.GetChild(i).position);
                if (distance < closetDistance)
                {
                    closetDistance = distance;
                    closestWayPointIndex = i;
                }
            }

            return closestWayPointIndex;
        }
    }
}