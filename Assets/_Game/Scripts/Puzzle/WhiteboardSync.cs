using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections.Generic;

namespace SubSurface.Puzzle
{
    /// <summary>
    /// Synchronisation réseau des traits de whiteboard.
    /// Le client dessinateur envoie ses points au serveur, qui broadcast aux autres.
    /// Les late-joiners reçoivent l'historique complet des traits.
    ///
    /// SETUP ÉDITEUR :
    /// → Attacher sur le parent Whiteboard (avec NetworkObject)
    /// → Inspector : _drawing = WhiteboardDrawing sur le BoardQuad enfant
    /// </summary>
    public class WhiteboardSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private WhiteboardDrawing _drawing;

        [Header("Settings")]
        [Tooltip("Nombre maximum de traits stockés (évite les fuites mémoire)")]
        [SerializeField] private int _maxStrokeHistory = 200;

        // Server-side : historique complet pour les late-joiners
        private readonly List<StrokeData> _strokeHistory = new();

        // Server-side : traits en cours (un par joueur potentiellement)
        private readonly Dictionary<int, StrokeData> _activeStrokes = new();

        // Client-side : dernier point reçu par stroke (pour dessiner les segments)
        private readonly Dictionary<int, Vector2> _lastRemotePoint = new();

        // =====================================================================
        // Data structures
        // =====================================================================

        /// <summary>Données d'un trait complet, stocké côté serveur.</summary>
        private struct StrokeData
        {
            public int StrokeId;
            public byte ColorIndex;
            public float BrushRadius;
            public List<Vector2> Points;
        }

        // =====================================================================
        // Late-joiner : demande l'historique au connect
        // =====================================================================

        public override void OnStartClient()
        {
            base.OnStartClient();
            CmdRequestHistory(base.LocalConnection);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdRequestHistory(NetworkConnection conn)
        {
            foreach (var stroke in _strokeHistory)
            {
                if (stroke.Points.Count == 0) continue;
                bool erasing = stroke.ColorIndex == 1;
                TargetReplayStroke(conn, stroke.StrokeId, stroke.BrushRadius, stroke.Points.ToArray(), erasing);
            }
        }

        [TargetRpc]
        private void TargetReplayStroke(NetworkConnection conn, int strokeId, float brushRadius, Vector2[] points, bool erasing)
        {
            if (_drawing != null)
                _drawing.ReplayStroke(points, brushRadius, erasing);
        }

        // =====================================================================
        // Real-time drawing : client → server → all clients
        // =====================================================================

        /// <summary>Début d'un nouveau trait. Appelé par WhiteboardDrawing local.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdBeginStroke(int strokeId, byte colorIndex, float brushRadius, NetworkConnection sender = null)
        {
            int key = GetStrokeKey(sender, strokeId);

            var stroke = new StrokeData
            {
                StrokeId = key,
                ColorIndex = colorIndex,
                BrushRadius = brushRadius,
                Points = new List<Vector2>()
            };
            _activeStrokes[key] = stroke;
        }

        /// <summary>Batch de points pendant le dessin. Appelé chaque frame par WhiteboardDrawing.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdAddPoints(int strokeId, Vector2[] uvs, NetworkConnection sender = null)
        {
            int key = GetStrokeKey(sender, strokeId);

            if (!_activeStrokes.TryGetValue(key, out var stroke)) return;

            foreach (var uv in uvs)
                stroke.Points.Add(uv);

            _activeStrokes[key] = stroke;

            int senderClientId = sender != null ? sender.ClientId : -1;
            bool erasing = stroke.ColorIndex == 1;
            ObserversDrawSegments(key, uvs, stroke.BrushRadius, senderClientId, erasing);
        }

        /// <summary>Fin du trait. Appelé par WhiteboardDrawing quand le joueur relâche le click.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void CmdEndStroke(int strokeId, NetworkConnection sender = null)
        {
            int key = GetStrokeKey(sender, strokeId);

            if (!_activeStrokes.TryGetValue(key, out var stroke)) return;

            _strokeHistory.Add(stroke);
            _activeStrokes.Remove(key);
            _lastRemotePoint.Remove(key);

            // Cap mémoire
            while (_strokeHistory.Count > _maxStrokeHistory)
                _strokeHistory.RemoveAt(0);
        }

        // =====================================================================
        // Broadcast aux clients
        // =====================================================================

        [ObserversRpc]
        private void ObserversDrawSegments(int strokeKey, Vector2[] uvs, float brushRadius, int senderClientId, bool erasing)
        {
            // Le dessinateur local skip (il a déjà dessiné côté client)
            if (base.ClientManager != null && base.ClientManager.Connection.ClientId == senderClientId)
                return;

            if (_drawing == null || uvs.Length == 0) return;

            // Premier point du stroke distant : stamp sans segment
            if (!_lastRemotePoint.TryGetValue(strokeKey, out var lastPt))
            {
                _drawing.DrawRemoteSegment(uvs[0], uvs[0], brushRadius, erasing);
                lastPt = uvs[0];

                if (uvs.Length == 1)
                {
                    _lastRemotePoint[strokeKey] = lastPt;
                    return;
                }
            }

            // Dessiner les segments
            for (int i = 0; i < uvs.Length; i++)
            {
                Vector2 from = i == 0 ? lastPt : uvs[i - 1];
                _drawing.DrawRemoteSegment(from, uvs[i], brushRadius, erasing);
            }

            _lastRemotePoint[strokeKey] = uvs[uvs.Length - 1];
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Clé unique par stroke en combinant clientId + strokeId local.
        /// Permet à plusieurs joueurs de dessiner en même temps sans collision.
        /// </summary>
        private int GetStrokeKey(NetworkConnection conn, int localStrokeId)
        {
            int clientId = conn != null ? conn.ClientId : 0;
            return clientId * 10000 + localStrokeId;
        }
    }
}
