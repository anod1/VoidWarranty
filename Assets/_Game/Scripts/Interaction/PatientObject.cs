// ============================================================================
// PatientObject.cs — Le système à "réparer" (ex: refroidissement du data center)
// ============================================================================
//
// State Machine à 4 états :
//
//   INFECTED ──[Toolbox + Interact]──► DISMANTLING ──[Timer fini]──► EMPTY
//       │                                  │                           │
//       │                            (pièce corrompue                  │
//       │                             éjectée ici)                     │
//       │                                                              │
//       │                                         [Pièce neuve + Interact]
//       │                                                              │
//       │                                                              ▼
//       │                                                          REPAIRED
//       │
//   Le joueur doit :
//     1. Avoir une Toolbox en main → Interact → lance le démontage
//     2. Rester proche pendant le timer
//     3. Récupérer la pièce corrompue éjectée
//     4. Prendre la pièce de rechange (depuis la SupplyCrate)
//     5. Interact avec la pièce en main → installation → REPAIRED
//
// Coop :
//   - Plusieurs joueurs peuvent réparer en même temps (vitesse augmentée)
//   - Si tout le monde s'éloigne, la progression reset (punitif)
//
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using VoidWarranty.Core;
using VoidWarranty.Player;
using System.Collections.Generic;

namespace VoidWarranty.Interaction
{
    public class PatientObject : NetworkBehaviour, IInteractable
    {
        // =====================================================================
        // États
        // =====================================================================

        public enum PatientState
        {
            Infected,       // Pièce corrompue en place, câbles noirs, fumée
            Dismantling,    // Timer en cours — démontage de la pièce corrompue
            Empty,          // Pièce corrompue retirée, en attente de la pièce neuve
            Repaired        // Pièce neuve installée, contrat validable
        }

        // =====================================================================
        // Inspector — Settings
        // =====================================================================

        [Header("Repair Settings")]
        [SerializeField] private float _baseRepairTime = 5f;
        [SerializeField] private float _advancedSpeedMultiplier = 2f;
        [SerializeField] private float _maxInteractionDistance = 4f;
        [SerializeField] private float _installDuration = 1.5f;

        [Header("Spawn — Pièce Corrompue")]
        [SerializeField] private GameObject _infectedNodePrefab;
        [SerializeField] private Transform  _nodeSpawnPoint;

        [Header("Accepted Spare Part Type")]
        [SerializeField] private ItemType _requiredSpareType = ItemType.Motor;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject    _cables;
        [SerializeField] private ParticleSystem _smokeEffect;
        [SerializeField] private GameObject    _infectedVisual;   // Mesh de la pièce corrompue en place
        [SerializeField] private GameObject    _repairedVisual;   // Mesh de la pièce neuve installée

        [Header("UI — Progress Bar (World Space)")]
        [SerializeField] private Canvas _progressBarCanvas;
        [SerializeField] private Image  _progressBarFill;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip   _dismantleLoopClip;
        [SerializeField] private AudioClip   _dismantleCompleteClip;
        [SerializeField] private AudioClip   _installClip;

        // =====================================================================
        // State
        // =====================================================================

        private PatientState _currentState = PatientState.Infected;
        private float _currentProgress = 0f;    // 0 à 1
        private float _installProgress = 0f;    // 0 à 1 (pour l'installation)
        private GameObject _installerPlayer;     // Le joueur qui installe la pièce neuve

        // Coop : joueurs en train de démonter + leur puissance d'outil
        private Dictionary<GameObject, float> _activeRepairers = new Dictionary<GameObject, float>();

        // =====================================================================
        // Init
        // =====================================================================

        private void Start()
        {
            if (_progressBarCanvas != null)
                _progressBarCanvas.enabled = false;

            if (_repairedVisual != null)
                _repairedVisual.SetActive(false);

            // L'état initial montre la pièce corrompue
            if (_infectedVisual != null)
                _infectedVisual.SetActive(true);
        }

        // =====================================================================
        // Update (Server Only)
        // =====================================================================

        private void Update()
        {
            if (!base.IsServerInitialized) return;

            switch (_currentState)
            {
                case PatientState.Dismantling:
                    UpdateDismantling();
                    break;

                case PatientState.Empty:
                    UpdateInstalling();
                    break;
            }
        }

        // =====================================================================
        // Interaction — Point d'entrée client
        // =====================================================================

        public void Interact(GameObject interactor)
        {
            // Vérifier ce que le joueur tient
            PlayerGrab grab = interactor.GetComponent<PlayerGrab>();
            GrabbableObject heldObject = (grab != null) ? grab.GetHeldObject() : null;

            CmdInteract(interactor, heldObject);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdInteract(GameObject interactor, GrabbableObject heldObject)
        {
            switch (_currentState)
            {
                case PatientState.Infected:
                    TryStartDismantle(interactor, heldObject);
                    break;

                case PatientState.Empty:
                    TryStartInstall(interactor, heldObject);
                    break;

                case PatientState.Dismantling:
                    // Un nouveau joueur rejoint le démontage en cours
                    TryJoinDismantle(interactor, heldObject);
                    break;

                case PatientState.Repaired:
                    // Rien à faire
                    break;
            }
        }

        // =====================================================================
        // Phase 1 — DÉMONTAGE (Infected → Dismantling → Empty)
        // =====================================================================

        private void TryStartDismantle(GameObject interactor, GrabbableObject tool)
        {
            float power = GetToolPower(tool);

            if (power <= 0f)
            {
                // Pas de toolbox → feedback au joueur
                ObserversShowFeedback(interactor, "FEEDBACK_NEED_TOOLBOX");
                return;
            }

            _activeRepairers[interactor] = power;
            _currentState = PatientState.Dismantling;
            _currentProgress = 0f;

            ObserversSetUIActive(true);
            ObserversPlayDismantleLoop(true);
        }

        private void TryJoinDismantle(GameObject interactor, GrabbableObject tool)
        {
            // Déjà en train de réparer ?
            if (_activeRepairers.ContainsKey(interactor)) return;

            float power = GetToolPower(tool);
            if (power <= 0f)
            {
                ObserversShowFeedback(interactor, "FEEDBACK_NEED_TOOLBOX");
                return;
            }

            _activeRepairers[interactor] = power;
        }

        private void UpdateDismantling()
        {
            // Calculer la vitesse totale (somme des puissances de tous les joueurs)
            float totalPower = 0f;
            List<GameObject> toRemove = new List<GameObject>();

            foreach (var kvp in _activeRepairers)
            {
                GameObject player = kvp.Key;
                float power = kvp.Value;

                // Vérifier que le joueur est encore là et proche
                if (player == null ||
                    Vector3.Distance(transform.position, player.transform.position) > _maxInteractionDistance)
                {
                    toRemove.Add(player);
                }
                else
                {
                    totalPower += power;
                }
            }

            // Retirer les joueurs partis
            foreach (var p in toRemove)
                _activeRepairers.Remove(p);

            // Plus personne → annuler et reset
            if (_activeRepairers.Count == 0)
            {
                CancelDismantle();
                return;
            }

            // Avancer la progression
            // Formule : (deltaTime / tempsDeBase) * puissanceTotale
            // 1 joueur basique = 1x, 1 joueur avancé = 2x, 2 joueurs basiques = 2x, etc.
            float progressStep = (Time.deltaTime / _baseRepairTime) * totalPower;
            _currentProgress += progressStep;

            ObserversUpdateProgress(_currentProgress);

            if (_currentProgress >= 1f)
            {
                CompleteDismantling();
            }
        }

        private void CancelDismantle()
        {
            _currentState = PatientState.Infected;
            _currentProgress = 0f;
            _activeRepairers.Clear();

            ObserversSetUIActive(false);
            ObserversPlayDismantleLoop(false);
        }

        private void CompleteDismantling()
        {
            _currentState = PatientState.Empty;
            _currentProgress = 0f;
            _activeRepairers.Clear();

            // Spawner la pièce corrompue
            if (_infectedNodePrefab != null && _nodeSpawnPoint != null)
            {
                GameObject instance = Instantiate(
                    _infectedNodePrefab,
                    _nodeSpawnPoint.position,
                    Quaternion.identity
                );
                Spawn(instance);
            }

            // Visuels côté réseau
            ObserversOnDismantleComplete();
        }

        [ObserversRpc]
        private void ObserversOnDismantleComplete()
        {
            // Retirer le visuel de la pièce corrompue
            if (_infectedVisual != null)
                _infectedVisual.SetActive(false);

            // Désactiver les câbles et la fumée
            if (_cables != null)
                _cables.SetActive(false);
            if (_smokeEffect != null)
                _smokeEffect.Stop();

            // Cacher la barre de progression
            if (_progressBarCanvas != null)
                _progressBarCanvas.enabled = false;

            // Son de complétion
            if (_audioSource != null && _dismantleCompleteClip != null)
                _audioSource.PlayOneShot(_dismantleCompleteClip);
        }

        // =====================================================================
        // Phase 2 — INSTALLATION (Empty → Repaired)
        // =====================================================================

        private void TryStartInstall(GameObject interactor, GrabbableObject heldObject)
        {
            if (heldObject == null)
            {
                ObserversShowFeedback(interactor, "FEEDBACK_NEED_SPARE_PART");
                return;
            }

            ItemData data = heldObject.GetData();
            if (data == null || data.Type != _requiredSpareType || data.IsDefective)
            {
                ObserversShowFeedback(interactor, "FEEDBACK_WRONG_PART");
                return;
            }

            // Le joueur a la bonne pièce ! On lui prend des mains.
            PlayerGrab grab = interactor.GetComponent<PlayerGrab>();
            if (grab != null)
                grab.ForceDrop();

            // Détruire la pièce de rechange (elle est "installée")
            if (heldObject.NetworkObject != null)
                heldObject.NetworkObject.Despawn();

            // Lancer le timer d'installation
            _installerPlayer = interactor;
            _installProgress = 0f;

            ObserversSetUIActive(true);
        }

        private void UpdateInstalling()
        {
            if (_installerPlayer == null)
            {
                CancelInstall();
                return;
            }

            // Vérifier la distance
            if (Vector3.Distance(transform.position, _installerPlayer.transform.position) > _maxInteractionDistance)
            {
                CancelInstall();
                return;
            }

            _installProgress += Time.deltaTime / _installDuration;
            ObserversUpdateProgress(_installProgress);

            if (_installProgress >= 1f)
            {
                CompleteInstall();
            }
        }

        private void CancelInstall()
        {
            // Note : la pièce est déjà détruite, donc on ne peut pas la rendre.
            // En phase 2, on pourrait la re-spawner. Pour l'instant, le joueur doit
            // rester proche pendant 1.5 secondes — c'est court, pas punitif.
            _installerPlayer = null;
            _installProgress = 0f;

            ObserversSetUIActive(false);
        }

        private void CompleteInstall()
        {
            _currentState = PatientState.Repaired;
            _installerPlayer = null;

            ObserversOnInstallComplete();
        }

        [ObserversRpc]
        private void ObserversOnInstallComplete()
        {
            // Afficher le visuel de la pièce neuve
            if (_repairedVisual != null)
                _repairedVisual.SetActive(true);

            // Cacher la barre
            if (_progressBarCanvas != null)
                _progressBarCanvas.enabled = false;

            // Son d'installation
            if (_audioSource != null && _installClip != null)
                _audioSource.PlayOneShot(_installClip);

            Debug.Log("✅ Patient réparé — pièce neuve installée !");
        }

        // =====================================================================
        // Utilitaires
        // =====================================================================

        /// <summary>
        /// Retourne la puissance de l'outil tenu. 0 = pas un outil valide.
        /// </summary>
        private float GetToolPower(GrabbableObject tool)
        {
            if (tool == null) return 0f;

            ItemData data = tool.GetData();
            if (data == null) return 0f;

            switch (data.Type)
            {
                case ItemType.Toolbox:         return 1f;
                case ItemType.ToolboxAdvanced:  return _advancedSpeedMultiplier;
                default:                        return 0f;
            }
        }

        // =====================================================================
        // Réseau — Broadcast
        // =====================================================================

        [ObserversRpc]
        private void ObserversUpdateProgress(float progress)
        {
            if (_progressBarCanvas != null)
            {
                if (!_progressBarCanvas.enabled)
                    _progressBarCanvas.enabled = true;

                if (_progressBarFill != null)
                    _progressBarFill.fillAmount = progress;
            }
        }

        [ObserversRpc]
        private void ObserversSetUIActive(bool isActive)
        {
            if (_progressBarCanvas != null)
                _progressBarCanvas.enabled = isActive;

            if (!isActive && _progressBarFill != null)
                _progressBarFill.fillAmount = 0f;
        }

        [ObserversRpc]
        private void ObserversPlayDismantleLoop(bool play)
        {
            if (_audioSource == null || _dismantleLoopClip == null) return;

            if (play)
            {
                _audioSource.clip = _dismantleLoopClip;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            else
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }
        }

        /// <summary>
        /// Feedback ciblé à un joueur spécifique (ex: "Il vous faut une boîte à outils").
        /// Pour l'instant on log. Plus tard → TargetRpc vers le HUD du joueur.
        /// </summary>
        [ObserversRpc]
        private void ObserversShowFeedback(GameObject target, string locKey)
        {
            // TODO: Remplacer par un TargetRpc quand le système de feedback HUD sera en place.
            // Pour l'instant, on log pour le debug.
            Debug.Log($"[Patient] Feedback pour {target.name}: {LocalizationManager.Get(locKey)}");
        }

        // =====================================================================
        // Prompt d'interaction
        // =====================================================================

        public string GetInteractionPrompt()
        {
            switch (_currentState)
            {
                case PatientState.Infected:
                    return LocalizationManager.Get("ACTION_DISMANTLE");
                    // Ex: "Démonter la pièce [Nécessite Boîte à Outils]"

                case PatientState.Dismantling:
                    return LocalizationManager.Get("ACTION_REPAIR_JOIN");
                    // Ex: "Rejoindre la réparation [E]"

                case PatientState.Empty:
                    return LocalizationManager.Get("ACTION_INSTALL_PART");
                    // Ex: "Installer la pièce de rechange [E]"

                case PatientState.Repaired:
                    return LocalizationManager.Get("STATUS_REPAIRED");
                    // Ex: "Système Réparé ✓"

                default:
                    return "";
            }
        }
    }
}
