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
        public SocketSender(PipeScheduler scheduler) : base(scheduler)
        {
        }

        public SocketAwaitableEventArgs SendAsync(Socket socket, in ReadOnlySequence<byte> buffers)
        {
            if (buffers.IsSingleSegment)
            {
                return SendAsync(socket, buffers.First);
            }

            if (!MemoryBuffer.Equals(Memory<byte>.Empty))
            {
                SetBuffer(null, 0, 0);
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
            // TODO: Consider clearing the buffer and buffer list before we put it back into the pool
            // it's a performance hit but it removes the confusion when looking at dumps to see this still
            // holder onto the buffer when it's back in the pool
        }

        private SocketAwaitableEventArgs SendAsync(Socket socket, ReadOnlyMemory<byte> memory)
        {
            // The BufferList getter is much less expensive then the setter.
            if (BufferList != null)
            {
                BufferList = null;
            }

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

            if (BufferList == null)
            {
                BufferList = new List<ArraySegment<byte>>();
            }
            else
            {
                // Buffers are pooled, so it's OK to root them until the next multi-buffer write.
                BufferList.Clear();
            }

            foreach (var b in buffer)
            {
                BufferList.Add(b.GetArray());
            }
        }
    }
}
