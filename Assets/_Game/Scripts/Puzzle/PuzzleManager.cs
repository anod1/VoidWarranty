using UnityEngine;
using UnityEngine.Events;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using VoidWarranty.Core;

namespace SubSurface.Puzzle
{
    /// <summary>
    /// Puzzle de chambres de ballast — style water jug (Signalis).
    /// 3 chambres (Rouge=7, Vert=5, Bleu=3), total constant = 8.
    /// 3 vannes en cycle fixe : R→V, V→B, B→R.
    /// Génération BFS garantie soluble en 3-4 étapes.
    ///
    /// SETUP ÉDITEUR :
    /// → GO vide "PuzzleManager" + NetworkObject + PuzzleManager.cs
    /// → Inspector : brancher OnPuzzleSolved → LevelDoor.UnlockAndOpen (×2 portes)
    /// → Inspector : OnFirstValveUsed vide (hook futur)
    /// </summary>
    public class PuzzleManager : NetworkBehaviour
    {
        public static PuzzleManager Instance { get; private set; }

        [Header("Difficulty")]
        [Tooltip("Distance BFS minimum (nombre de transferts pour résoudre)")]
        [SerializeField] private int _minBfsDistance = 5;
        [Tooltip("Distance BFS maximum")]
        [SerializeField] private int _maxBfsDistance = 8;

        [Header("Events")]
        public UnityEvent OnPuzzleSolved;
        public UnityEvent OnFirstValveUsed;

        // Capacités fixes des chambres
        private static readonly int[] Capacities = { 7, 5, 3 };
        private static readonly string[] ChamberKeys = { "PUZZLE_CHAMBER_RED", "PUZZLE_CHAMBER_GREEN", "PUZZLE_CHAMBER_BLUE" };
        private const int TotalPressure = 8;

        // 3 transferts du cycle fixe : R→V, V→B, B→R
        private static readonly int[,] ValidTransfers = { { 0, 1 }, { 1, 2 }, { 2, 0 } };

        // SyncVars — pressions actuelles
        private readonly SyncVar<int> _pressureRed = new();
        private readonly SyncVar<int> _pressureGreen = new();
        private readonly SyncVar<int> _pressureBlue = new();

        // SyncVars — pressions cibles
        private readonly SyncVar<int> _targetRed = new();
        private readonly SyncVar<int> _targetGreen = new();
        private readonly SyncVar<int> _targetBlue = new();

        // SyncVar — résolu
        private readonly SyncVar<bool> _isSolved = new();

        private int _transferCount;
        private AudioSource _audioSource;

        // =====================================================================
        // Accesseurs publics (lecture UI)
        // =====================================================================

        public bool IsSolved => _isSolved.Value;

        public int GetPressure(int chamber)
        {
            return chamber switch
            {
                0 => _pressureRed.Value,
                1 => _pressureGreen.Value,
                2 => _pressureBlue.Value,
                _ => 0
            };
        }

        public int GetTarget(int chamber)
        {
            return chamber switch
            {
                0 => _targetRed.Value,
                1 => _targetGreen.Value,
                2 => _targetBlue.Value,
                _ => 0
            };
        }

        public static int GetCapacity(int chamber)
        {
            if (chamber < 0 || chamber > 2) return 0;
            return Capacities[chamber];
        }

        public static string GetChamberName(int chamber)
        {
            if (chamber < 0 || chamber > 2) return "?";
            return LocalizationManager.Get(ChamberKeys[chamber]);
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = GetComponentInChildren<AudioSource>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            GeneratePuzzle();
        }

        // =====================================================================
        // Génération BFS
        // =====================================================================

        [Server]
        private void GeneratePuzzle()
        {
            const int maxAttempts = 100;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 1. État initial aléatoire valide
                int[] initial = GenerateRandomState();

                // 2. BFS depuis cet état
                var distances = BFS(initial);

                // 3. Filtrer par distance configurable
                var candidates = new List<int[]>();
                foreach (var kvp in distances)
                {
                    if (kvp.Value >= _minBfsDistance && kvp.Value <= _maxBfsDistance)
                        candidates.Add(DecodeState(kvp.Key));
                }

                if (candidates.Count == 0) continue;

                // 4. Tirer une cible aléatoire
                int[] target = candidates[Random.Range(0, candidates.Count)];

                // 5. Appliquer
                SetPressures(initial);
                SetTargets(target);
                _isSolved.Value = false;
                _transferCount = 0;

                Debug.Log($"[PuzzleManager] Puzzle généré :" +
                    $" Initial=({initial[0]},{initial[1]},{initial[2]})" +
                    $" Cible=({target[0]},{target[1]},{target[2]})" +
                    $" Distance={distances[EncodeState(target)]}");
                return;
            }

            // Fallback : config fixe soluble en 5 steps
            Debug.LogWarning("[PuzzleManager] BFS fallback : config fixe.");
            SetPressures(new[] { 6, 2, 0 });
            SetTargets(new[] { 1, 4, 3 });
            _isSolved.Value = false;
            _transferCount = 0;
        }

        private int[] GenerateRandomState()
        {
            // Distribution aléatoire de TotalPressure unités dans 3 chambres
            // respectant les capacités max
            int[] state = new int[3];
            int remaining = TotalPressure;

            for (int i = 0; i < 2; i++)
            {
                int maxHere = Mathf.Min(remaining, Capacities[i]);
                state[i] = Random.Range(0, maxHere + 1);
                remaining -= state[i];
            }

            // Dernière chambre prend le reste (si ça dépasse sa capacité, retry)
            state[2] = remaining;
            if (state[2] > Capacities[2])
                return GenerateRandomState(); // Retry récursif (rare)

            return state;
        }

        /// <summary>
        /// BFS : explore tous les états atteignables depuis initial.
        /// Retourne un dictionnaire encodedState → distance.
        /// </summary>
        private Dictionary<int, int> BFS(int[] initial)
        {
            var distances = new Dictionary<int, int>();
            var queue = new Queue<int>();

            int startKey = EncodeState(initial);
            distances[startKey] = 0;
            queue.Enqueue(startKey);

            while (queue.Count > 0)
            {
                int currentKey = queue.Dequeue();
                int[] current = DecodeState(currentKey);
                int dist = distances[currentKey];

                // Pas besoin d'explorer au-delà du max configuré
                if (dist >= _maxBfsDistance) continue;

                // 3 transferts possibles (cycle fixe)
                for (int t = 0; t < 3; t++)
                {
                    int from = ValidTransfers[t, 0];
                    int to = ValidTransfers[t, 1];

                    if (current[from] == 0) continue; // Rien à transférer
                    if (current[to] >= Capacities[to]) continue; // Cible pleine

                    // Calculer le transfert
                    int[] next = (int[])current.Clone();
                    int transfer = Mathf.Min(next[from], Capacities[to] - next[to]);
                    next[from] -= transfer;
                    next[to] += transfer;

                    int nextKey = EncodeState(next);
                    if (!distances.ContainsKey(nextKey))
                    {
                        distances[nextKey] = dist + 1;
                        queue.Enqueue(nextKey);
                    }
                }
            }

            return distances;
        }

        // Encodage compact : état (r, v, b) → entier unique
        // r ∈ [0,7], v ∈ [0,5], b ∈ [0,3] → r*24 + v*4 + b (max = 7*24+5*4+3 = 191)
        private static int EncodeState(int[] s) => s[0] * 24 + s[1] * 4 + s[2];
        private static int[] DecodeState(int key) => new[] { key / 24, (key % 24) / 4, key % 4 };

        // =====================================================================
        // Transfert de pression (appelé par ValveInteractable)
        // =====================================================================

        /// <summary>
        /// Exécute un transfert atomique. Valide la paire from/to.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdTransferPressure(int from, int to)
        {
            if (_isSolved.Value) return;

            // Valider la paire (doit être un des 3 transferts du cycle)
            bool valid = false;
            for (int t = 0; t < 3; t++)
            {
                if (ValidTransfers[t, 0] == from && ValidTransfers[t, 1] == to)
                {
                    valid = true;
                    break;
                }
            }
            if (!valid)
            {
                Debug.LogWarning($"[PuzzleManager] Transfert invalide : {from}→{to}");
                return;
            }

            int[] pressures = { _pressureRed.Value, _pressureGreen.Value, _pressureBlue.Value };

            if (pressures[from] == 0 || pressures[to] >= Capacities[to])
            {
                ObserversTransferFeedback(from, to, false);
                return;
            }

            // Transfert atomique : écouler jusqu'à source vide ou cible pleine
            int transfer = Mathf.Min(pressures[from], Capacities[to] - pressures[to]);
            pressures[from] -= transfer;
            pressures[to] += transfer;

            SetPressures(pressures);

            // Premier transfert → event
            if (_transferCount == 0)
                OnFirstValveUsed?.Invoke();
            _transferCount++;

            ObserversTransferFeedback(from, to, true);

            Debug.Log($"[PuzzleManager] Transfert {GetChamberName(from)}→{GetChamberName(to)}" +
                $" ({transfer} unités) → ({pressures[0]},{pressures[1]},{pressures[2]})");

            // Vérifier victoire
            if (CheckWin())
            {
                _isSolved.Value = true;
                Debug.Log("[PuzzleManager] Puzzle résolu !");
                OnPuzzleSolved?.Invoke();
                ObserversPuzzleSolved();
            }
        }

        [Server]
        private bool CheckWin()
        {
            return _pressureRed.Value == _targetRed.Value
                && _pressureGreen.Value == _targetGreen.Value
                && _pressureBlue.Value == _targetBlue.Value;
        }

        [Server]
        private void SetPressures(int[] p)
        {
            _pressureRed.Value = p[0];
            _pressureGreen.Value = p[1];
            _pressureBlue.Value = p[2];
        }

        [Server]
        private void SetTargets(int[] t)
        {
            _targetRed.Value = t[0];
            _targetGreen.Value = t[1];
            _targetBlue.Value = t[2];
        }

        // =====================================================================
        // Feedback réseau
        // =====================================================================

        [ObserversRpc]
        private void ObserversTransferFeedback(int from, int to, bool success)
        {
            // Les ValveInteractable écoutent cet événement pour animer
            OnTransferResult?.Invoke(from, to, success);
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversPuzzleSolved()
        {
            OnPuzzleSolvedNetwork?.Invoke();
        }

        // Events pour que les ValveInteractable et PuzzleConsoleUI puissent réagir
        public static event System.Action<int, int, bool> OnTransferResult;
        public static event System.Action OnPuzzleSolvedNetwork;
    }
}
