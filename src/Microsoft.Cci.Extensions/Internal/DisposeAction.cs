// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System
{
    // Helper that could be part of the BCL
    internal class DisposeAction : IDisposable
    {
        private Action _action;
        public DisposeAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            if (_action != null)
            {
                _action();
                _action = null;
            }
        }
    }
}
