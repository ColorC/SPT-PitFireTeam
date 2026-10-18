using Comfort.Common;
using Diz.LanguageExtensions;
using DrakiaXYZ.BigBrain.Brains;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using EFT.UI;
using EFT.UI.DragAndDrop;
using JsonType;
using pitTeam.Components;
using pitTeam.Modules;
using pitTeam.Patches;
using pitTeam.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace pitTeam.BigBrain.Actions
{
    internal partial class GestureCommandAction
    {
        private Il2CppSystem.Action? _doorInteractEndedToken;
        private BotDoorOpener? _doorEventOwner;
        private void HandleOpenDoor()
        {
            activeDoor ??= InteractableObjects.GetDoorToOpen(BotOwner);
            if (activeDoor == null)
            {
                ClearOpenDoorState("OpenDoor:missingDoor");
                return;
            }

            if (activeDoor.DoorState == EDoorState.Open)
            {
                ClearOpenDoorState("OpenDoor:alreadyOpen");
                return;
            }

            BotOwner.DoorOpener.UpdateDoorInteractionStatus();

            if (!doorMoveIssued)
            {
                Vector3 position = activeDoor.transform.position;
                if (!NavMesh.SamplePosition(position, out NavMeshHit navMeshHit, 2f, NavMesh.AllAreas))
                {
                    ClearOpenDoorState("OpenDoor:noNavMesh");
                    return;
                }

                if (BotOwner.GoToPoint(navMeshHit.position, false, -1f, false, false) != NavMeshPathStatus.PathComplete)
                {
                    ClearOpenDoorState("OpenDoor:pathInvalid");
                    return;
                }

                BotOwner.GoToSomePointData.SetPoint(navMeshHit.position);
                BotOwner.Steering.LookToMovingDirection();
                doorMoveIssued = true;
                doorTimeoutAt = Time.time + 7f;
                return;
            }

            if (doorTimeoutAt > 0f && Time.time > doorTimeoutAt)
            {
                ClearOpenDoorState("OpenDoor:timeout");
                return;
            }

            if (!doorInteractIssued)
            {
                BotOwner.GoToSomePointData.UpdateToGo(false);
            }

            if (!BotOwner.GoToSomePointData.IsCome())
            {
                return;
            }

            if (doorInteractIssued)
            {
                return;
            }

            BotOwner.StopMove();
            _doorInteractEndedToken ??= (Il2CppSystem.Action)(System.Action)OnDoorInteractEnded;
            if (_doorEventOwner != null) _doorEventOwner.OnEndInteract -= _doorInteractEndedToken;
            _doorEventOwner = BotOwner.DoorOpener;
            _doorEventOwner.OnEndInteract += _doorInteractEndedToken;
            BotOwner.DoorOpener.Interact(activeDoor, EInteractionType.Open);
            doorInteractIssued = true;
        }

        private void OnDoorInteractEnded()
        {
            ClearOpenDoorState("OpenDoor:done");
        }

        private void CleanupDoorInteraction()
        {
            if (_doorEventOwner != null && _doorInteractEndedToken != null)
            {
                _doorEventOwner.OnEndInteract -= _doorInteractEndedToken;
            }
            _doorEventOwner = null;

            activeDoor = null;
            doorMoveIssued = false;
            doorInteractIssued = false;
            doorTimeoutAt = 0f;
        }

        private void ClearOpenDoorState(string reason)
        {
            CleanupDoorInteraction();
            InteractableObjects.RemoveOpener(BotOwner);
            InteractableObjects.SetCurDoor(null);
            followerData?.ClearCommand(reason);
        }
    }
}
