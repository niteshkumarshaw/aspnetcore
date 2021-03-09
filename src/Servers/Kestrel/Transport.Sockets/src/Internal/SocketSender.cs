// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal
{
    internal sealed class SocketSender : SocketAwaitableEventArgs
    {
        private List<ArraySegment<byte>>? _bufferList;

        public SocketSender(PipeScheduler scheduler) : base(scheduler)
        {
        }

        public SocketAwaitableEventArgs SendAsync(Socket socket, in ReadOnlySequence<byte> buffers)
        {
            if (buffers.IsSingleSegment)
            {
                return SendAsync(socket, buffers.First);
            }

            SetBufferList(buffers);

            if (!socket.SendAsync(this))
            {
                Complete();
            }

            return this;
        }

        public void Reset()
        {
            // We clear the buffer and buffer list before we put it back into the pool
            // it's a small performance hit but it removes the confusion when looking at dumps to see this still
            // holder onto the buffer when it's back in the pool
            BufferList = null;

            SetBuffer(null, 0, 0);

            _bufferList?.Clear();
        }

        private SocketAwaitableEventArgs SendAsync(Socket socket, ReadOnlyMemory<byte> memory)
        {
            SetBuffer(MemoryMarshal.AsMemory(memory));

            if (!socket.SendAsync(this))
            {
                Complete();
            }

            return this;
        }

        private void SetBufferList(in ReadOnlySequence<byte> buffer)
        {
            Debug.Assert(!buffer.IsEmpty);
            Debug.Assert(!buffer.IsSingleSegment);

            if (_bufferList == null)
            {
                _bufferList = new List<ArraySegment<byte>>();
            }
            else
            {
                // Buffers are pooled, so it's OK to root them until the next multi-buffer write.
                _bufferList.Clear();
            }

            foreach (var b in buffer)
            {
                _bufferList.Add(b.GetArray());
            }

            // The act of setting this list, sets the buffers in the internal buffer list
            BufferList = _bufferList;
        }
    }
}
