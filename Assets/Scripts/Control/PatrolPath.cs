namespace RPG.Control
{
    using UnityEngine;

    public class PatrolPath : MonoBehaviour
    {
        const float wayPointGizmoRadius = 0.3f;

        private void OnDrawGizmos()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Gizmos.DrawSphere(GetWaypoint(i), wayPointGizmoRadius);
                Gizmos.DrawLine(GetWaypoint(i), GetNextWayPoint(i));
            }
        }

        private Vector3 GetWaypoint(int i)
        {
            return transform.GetChild(i).position;
        }

        private Vector3 GetNextWayPoint(int i)
        {
            return GetWaypoint(i + 1 < transform.childCount ? i + 1 : 0);
        }
    }
}