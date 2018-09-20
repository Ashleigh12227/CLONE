﻿using System.Linq;
using Quasar.Common.Messages;
using xServer.Core.Commands;

namespace xServer.Core.Networking
{
    public class QuasarServer : Server
    {
        /// <summary>
        /// Gets the clients currently connected and authenticated to the server.
        /// </summary>
        public Client[] ConnectedClients
        {
            get { return Clients.Where(c => c != null && c.Authenticated).ToArray(); }
        }

        /// <summary>
        /// Occurs when a client connected.
        /// </summary>
        public event ClientConnectedEventHandler ClientConnected;

        /// <summary>
        /// Represents the method that will handle the connected client.
        /// </summary>
        /// <param name="client">The connected client.</param>
        public delegate void ClientConnectedEventHandler(Client client);

        /// <summary>
        /// Fires an event that informs subscribers that the client is connected.
        /// </summary>
        /// <param name="client">The connected client.</param>
        private void OnClientConnected(Client client)
        {
            if (ProcessingDisconnect || !Listening) return;
            var handler = ClientConnected;
            if (handler != null)
            {
                handler(client);
            }
        }

        /// <summary>
        /// Occurs when a client disconnected.
        /// </summary>
        public event ClientDisconnectedEventHandler ClientDisconnected;

        /// <summary>
        /// Represents the method that will handle the disconnected client.
        /// </summary>
        /// <param name="client">The disconnected client.</param>
        public delegate void ClientDisconnectedEventHandler(Client client);

        /// <summary>
        /// Fires an event that informs subscribers that the client is disconnected.
        /// </summary>
        /// <param name="client">The disconnected client.</param>
        private void OnClientDisconnected(Client client)
        {
            if (ProcessingDisconnect || !Listening) return;
            var handler = ClientDisconnected;
            if (handler != null)
            {
                handler(client);
            }
        }

        /// <summary>
        /// Constructor, initializes required objects and subscribes to events of the server.
        /// </summary>
        public QuasarServer() : base()
        {
            base.ClientState += OnClientState;
            base.ClientRead += OnClientRead;
        }

        /// <summary>
        /// Decides if the client connected or disconnected.
        /// </summary>
        /// <param name="server">The server the client is connected to.</param>
        /// <param name="client">The client which changed its state.</param>
        /// <param name="connected">True if the client connected, false if disconnected.</param>
        private void OnClientState(Server server, Client client, bool connected)
        {
            switch (connected)
            {
                case true:
                    client.Send(new GetAuthentication()); // begin handshake
                    break;
                case false:
                    if (client.Authenticated)
                    {
                        OnClientDisconnected(client);
                    }
                    break;
            }
        }

        /// <summary>
        /// Forwards received messages from the client to the MessageHandler.
        /// </summary>
        /// <param name="server">The server the client is connected to.</param>
        /// <param name="client">The client which has received the message.</param>
        /// <param name="message">The received message.</param>
        private void OnClientRead(Server server, Client client, IMessage message)
        {
            var type = message.GetType();

            if (!client.Authenticated)
            {
                if (type == typeof (GetAuthenticationResponse))
                {
                    client.Authenticated = true;
                    client.Send(new SetAuthenticationSuccess()); // finish handshake
                    CommandHandler.HandleGetAuthenticationResponse(client,
                        (GetAuthenticationResponse)message);
                    OnClientConnected(client);
                }
                else
                {
                    client.Disconnect();
                }
                return;
            }

            MessageHandler.Process(client, message);
            PacketHandler.HandlePacket(client, message); // TODO: Remove this
        }
    }
}
