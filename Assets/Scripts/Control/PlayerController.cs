using UnityEngine;
using RPG.Movement;
using RPG.Combat;
using System;
using RPG.Core;
using RPG.Gathering;
using RPG.Items;

namespace RPG.Control
{
    public class PlayerController : MonoBehaviour
    {
        Fighter fighter;
        Mover mover;
        CombatTarget combatTarget;
        Health health;
        Gatherer gatherer;
        PickupCollector collector;

        private void Start()
        {
            fighter = GetComponent<Fighter>();
            mover = GetComponent<Mover>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (health.IsDead()) return;
            if (UiState.Blocked) return;
            if (InteractWithKeyboard()) return;
            if (UiState.PointerOverUi) return;
            if (InteractWithCombat()) return;
            if (InteractWithWorld()) return;
            if (InteractWithMovement()) return;
        }

        // WASD moves relative to the camera.
        private bool InteractWithKeyboard()
        {
            if (UiState.Typing) return false;
            float x = 0, z = 0;
            if (Input.GetKey(KeyCode.W)) z += 1;
            if (Input.GetKey(KeyCode.S)) z -= 1;
            if (Input.GetKey(KeyCode.D)) x += 1;
            if (Input.GetKey(KeyCode.A)) x -= 1;
            if (x == 0 && z == 0) return false;
            FollowCamera view = FollowCamera.Instance;
            Vector3 forward = view != null ? view.FlatForward : Vector3.forward;
            Vector3 right = view != null ? view.FlatRight : Vector3.right;
            mover.DirectMove(forward * z + right * x);
            return true;
        }

        private bool InteractWithCombat()
        {
            RaycastHit[] hits = Physics.RaycastAll(GetMouseRay());
            foreach (RaycastHit hit in hits)
            {
                CombatTarget target = hit.transform.GetComponent<CombatTarget>();
                if (target == null) continue;

                if (!fighter.CanAttack(target.gameObject)) continue;

                if (Input.GetMouseButton(0))
                {
                    fighter.Attack(target.gameObject);
                }
                return true;
            }
            return false;
        }

        // Loot pickups and resource nodes (trees, rocks).
        private bool InteractWithWorld()
        {
            if (gatherer == null) gatherer = GetComponent<Gatherer>();
            if (collector == null) collector = GetComponent<PickupCollector>();

            RaycastHit[] hits = Physics.RaycastAll(GetMouseRay());
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Pickup pickup = hit.transform.GetComponent<Pickup>();
                if (pickup != null && collector != null)
                {
                    if (Input.GetMouseButtonDown(0)) collector.Collect(pickup);
                    return true;
                }

                RPG.World.Station station = hit.transform.GetComponentInParent<RPG.World.Station>();
                if (station != null)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        RPG.World.StationUser user = GetComponent<RPG.World.StationUser>();
                        if (user != null) user.Use(station);
                    }
                    return true;
                }

                ResourceNode node = hit.transform.GetComponentInParent<ResourceNode>();
                if (node != null && gatherer != null)
                {
                    if (Input.GetMouseButtonDown(0)) gatherer.Gather(node);
                    return true;
                }
            }
            return false;
        }

        private bool InteractWithMovement()
        {
            RaycastHit hit;
            bool hasHit = Physics.Raycast(GetMouseRay(), out hit);

            if (hasHit)
            {
                if (Input.GetMouseButton(0))
                {
                    mover.StartMoveAction(hit.point, 1f);
                    return true;
                }
            }
            return false;
        }

        private static Ray GetMouseRay()
        {
            return Camera.main.ScreenPointToRay(Input.mousePosition);
        }
    }
}
