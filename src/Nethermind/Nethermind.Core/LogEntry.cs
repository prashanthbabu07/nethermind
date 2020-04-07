//  Copyright (c) 2018 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System;
using Nethermind.Core.Crypto;

namespace Nethermind.Core
{
    public class LogEntry
    {
        public LogEntry(Address address, byte[] data, Keccak[] topics)
        {
            LoggersAddress = address;
            Data = data;
            Topics = topics;
        }

        public Address LoggersAddress { get; }
        public Keccak[] Topics { get; }
        public byte[] Data { get; }
        
        public static LogEntry[] EmptyLogs = new LogEntry[0];
    }
    
    public ref struct LogEntryRef
    {
        public LogEntryRef(AddressRef address, Span<byte> data, Span<byte> topics)
        {
            LoggersAddress = address;
            Data = data;
            Topics = topics;
        }

        public AddressRef LoggersAddress;

        /// <summary>
        /// Rlp encoded array of Keccak
        /// </summary>
        public Span<byte> Topics { get; }

        public Span<byte> Data { get; }
    }
}