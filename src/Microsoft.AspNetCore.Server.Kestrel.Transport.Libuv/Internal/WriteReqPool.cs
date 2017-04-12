// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal.Networking;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal
{
    public class WriteReqPool
    {
        private const int _maxPooledWriteReqs = 1024;

        private readonly LibuvThread _thread;
        private UvWriteReqEntry[] _pool;
        private int _nextFreeSlot = -1;

        private readonly ILibuvTrace _log;
        private bool _disposed;

        public WriteReqPool(LibuvThread thread, ILibuvTrace log)
        {
            _thread = thread;
            _log = log;
        }

        public UvWriteReqEntry Allocate()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_nextFreeSlot != -1)
            {
                var entry = _pool[_nextFreeSlot];
                entry.Used = true;
                return entry;
            }

            var start = 0;
            // Initial pool
            if (_pool == null)
            {
                _pool = new UvWriteReqEntry[_maxPooledWriteReqs];
            }
            else
            {
                // Double the size of the pool (should be rare hopefully), today
                // we don't do any shrinking
                var old = _pool;
                _pool = new UvWriteReqEntry[_maxPooledWriteReqs * 2];
                Array.Copy(old, 0, _pool, 0, old.Length);
                start = old.Length;
            }

            for (int i = start; i < _maxPooledWriteReqs; i++)
            {
                var req = new UvWriteReq(_log);
                req.Init(_thread.Loop);
                _pool[i] = new UvWriteReqEntry(req, i);
            }

            _nextFreeSlot = start + 1;
            _pool[start].Used = true;
            return _pool[start];
        }

        public void Return(ref UvWriteReqEntry entry)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            entry.Used = false;
            _nextFreeSlot = entry.Index;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                for (int i = 0; i < _pool.Length; i++)
                {
                    _pool[i].Request.Dispose();
                }

                _pool = null;
            }
        }

        public struct UvWriteReqEntry
        {
            public UvWriteReq Request;
            public bool Used;
            public int Index;

            public UvWriteReqEntry(UvWriteReq req, int index)
            {
                Request = req;
                Used = false;
                Index = index;
            }
        }
    }
}
