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
    }
}