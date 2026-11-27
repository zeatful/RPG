using RPG.Core;
using RPG.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.World
{
    /// <summary>Player action: walk up to a station and open it. The HUD draws the open station's panel.</summary>
    public class StationUser : MonoBehaviour, IAction
    {
        public Station Open { get; private set; }

        Station target;
        Mover mover;
        ActionScheduler scheduler;
        Health health;

        private void Awake()
        {
            mover = GetComponent<Mover>();
            scheduler = GetComponent<ActionScheduler>();
            health = GetComponent<Health>();
        }

        public void Use(Station station)
        {
            if (station == null) return;
            scheduler.StartAction(this);
            target = station;
        }

        public void Close()
        {
            Open = null;
        }

        private void Update()
        {
            if (health != null && health.IsDead())
            {
                target = null;
                Open = null;
                return;
            }
            if (Open != null && (Open == null || !Open.isActiveAndEnabled || Open.DistanceTo(transform.position) > Station.UseRange + 2.5f)) Open = null;
            if (target == null) return;

            if (target.DistanceTo(transform.position) > Station.UseRange)
            {
                Vector3 point = target.ApproachPoint(transform.position);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(point, out hit, 3f, NavMesh.AllAreas)) point = hit.position;
                mover.MoveTo(point, 1f);
                return;
            }

            mover.Cancel();
            Vector3 look = target.transform.position;
            look.y = transform.position.y;
            if ((look - transform.position).sqrMagnitude > 0.01f) transform.LookAt(look);
            if (target.Kind == RPG.Items.StationKind.Door)
            {
                Doorway door = target.GetComponent<Doorway>();
                target = null;
                if (InteriorDirector.Instance != null) InteriorDirector.Instance.Travel(door);
                return;
            }
            Open = target;
            target = null;
        }

        public void Cancel()
        {
            target = null;
        }
    }
}
