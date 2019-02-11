﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Net;
using Microsoft.VisualBasic.CompilerServices;
using Nethermind.Core.Crypto;

namespace Nethermind.Stats.Model
{
    public class Node
    {
        private PublicKey _id;

        public PublicKey Id
        {
            get => _id;
            set
            {
                if (_id != null)
                {
                    throw new InvalidOperationException($"ID already set for the node {Id}");
                }

                _id = value;
                IdHash = Keccak.Compute(_id.PrefixedBytes);
            }
        }

        public Keccak IdHash { get; private set; }
        public string Host { get; private set; }
        public int Port { get; set; }
        public IPEndPoint Address { get; private set; }
        public bool AddedToDiscovery { get; set; }
        public bool IsBootnode { get; set; }
        public bool IsTrusted { get; set; }

        public bool IsStatic { get; set; }

        public string ClientId { get; set; }

        public Node(PublicKey id, IPEndPoint address)
        {
            Id = id;
            AddedToDiscovery = false;
            InitializeAddress(address);
        }

        public Node(PublicKey id, string host, int port, bool addedToDiscovery = false)
        {
            Id = id;
            AddedToDiscovery = addedToDiscovery;
            InitializeAddress(host, port);
        }

        public Node(string host, int port)
        {
            Keccak512 socketHash = Keccak512.Compute($"{host}:{port}");
            Id = new PublicKey(socketHash.Bytes);
            AddedToDiscovery = true;
            InitializeAddress(host, port);
        }

        public void InitializeAddress(IPEndPoint address)
        {
            Host = address.Address.ToString();
            Port = address.Port;
            Address = address;
        }

        public void InitializeAddress(string host, int port)
        {
            Host = host;
            Port = port;
            Address = new IPEndPoint(IPAddress.Parse(host), port);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is Node item)
            {
                return IdHash.Equals(item.IdHash);
            }

            return false;
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return IdHash.GetHashCode();
        }

        public override string ToString()
        {
            return $"Id: {Id}, Host: {Host}, RemotePort: {Port}, IsDiscovery: {AddedToDiscovery}";
        }
        
        public static bool operator ==(Node a, Node b)
        {
            if (ReferenceEquals(a, null))
            {
                return ReferenceEquals(b, null);
            }

            if (ReferenceEquals(b, null))
            {
                return false;
            }

            return a.Id.Equals(b.Id);
        }

        public static bool operator !=(Node a, Node b)
        {
            return !(a == b);
        }
    }
}