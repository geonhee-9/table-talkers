using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using TableTalkers.Core;

namespace TableTalkers.UI
{
    /// <summary>
    /// In-room text chat over NGO RPC (link sharing, quiet participation). One-shot events —
    /// history is kept locally per client, nothing chat-related is synced state.
    /// Attach to the player prefab next to NetworkParticipant.
    /// </summary>
    public sealed class ChatChannel : NetworkBehaviour
    {
        public const int MaxMessageLength = 200;
        private const int HistoryLimit = 50;

        public readonly struct ChatMessage
        {
            public readonly string SenderName;
            public readonly string Text;

            public ChatMessage(string senderName, string text)
            {
                SenderName = senderName;
                Text = text;
            }
        }

        /// <summary>Local chat history (latest last), shared across channel instances.</summary>
        public static readonly List<ChatMessage> History = new();

        public static event Action<ChatMessage> MessageReceived;

        private IParticipant _participant;

        public override void OnNetworkSpawn()
        {
            _participant = GetComponent<IParticipant>();
        }

        /// <summary>Send a chat line as the local player (owner only).</summary>
        public void Send(string text)
        {
            if (!IsSpawned || !IsOwner || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            text = text.Trim();
            if (text.Length > MaxMessageLength)
            {
                text = text.Substring(0, MaxMessageLength);
            }

            SendMessageServerRpc(text);
        }

        [ServerRpc]
        private void SendMessageServerRpc(FixedString512Bytes text)
        {
            BroadcastMessageClientRpc(text);
        }

        [ClientRpc]
        private void BroadcastMessageClientRpc(FixedString512Bytes text)
        {
            // Drop lines from blocked participants at the receiving end.
            if (_participant != null
                && TableTalkers.Moderation.BlockList.IsBlocked(_participant.Id))
            {
                return;
            }

            var message = new ChatMessage(_participant?.DisplayName ?? "?", text.ToString());
            History.Add(message);
            if (History.Count > HistoryLimit)
            {
                History.RemoveAt(0);
            }

            MessageReceived?.Invoke(message);
        }
    }
}
