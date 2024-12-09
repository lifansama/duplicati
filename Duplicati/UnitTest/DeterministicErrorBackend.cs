﻿//  Copyright (C) 2015, The Duplicati Team
//  http://www.duplicati.com, info@duplicati.com
//
//  This library is free software; you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as
//  published by the Free Software Foundation; either version 2.1 of the
//  License, or (at your option) any later version.
//
//  This library is distributed in the hope that it will be useful, but
//  WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//  Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public
//  License along with this library; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
using Duplicati.Library.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Duplicati.UnitTest
{
    public class DeterministicErrorBackend : IBackend, IStreamingBackend
    {
        public class DeterministicErrorBackendException(string message) : Exception(message) { };

        static DeterministicErrorBackend() { WrappedBackend = "file"; }

        private static readonly Random random = new Random(42);

        // return true to throw exception, parameters: (action, remotename)
        public static Func<string, string, bool> ErrorGenerator = null;

        public static string WrappedBackend { get; set; }

        private IStreamingBackend m_backend;
        public DeterministicErrorBackend()
        {
        }

        // ReSharper disable once UnusedMember.Global
        public DeterministicErrorBackend(string url, Dictionary<string, string> options)
        {
            var u = new Library.Utility.Uri(url).SetScheme(WrappedBackend).ToString();
            m_backend = (IStreamingBackend)Library.DynamicLoader.BackendLoader.GetBackend(u, options);
        }

        private void ThrowError(string action, string remotename)
        {
            if (ErrorGenerator != null && ErrorGenerator(action, remotename))
            {
                throw new DeterministicErrorBackendException("Backend error");
            }
        }
        #region IStreamingBackend implementation
        public async Task PutAsync(string remotename, Stream stream, CancellationToken cancelToken)
        {
            var uploadError = random.NextDouble() > 0.9;

            using (var f = new Library.Utility.ProgressReportingStream(stream, x => { if (uploadError && stream.Position > stream.Length / 2) throw new DeterministicErrorBackendException("Random upload failure"); }))
                await m_backend.PutAsync(remotename, f, cancelToken).ConfigureAwait(false);
            ThrowError("put_async", remotename);
        }
        public async Task GetAsync(string remotename, Stream stream, CancellationToken cancellationToken)
        {
            ThrowError("get_0", remotename);
            await m_backend.GetAsync(remotename, stream, cancellationToken).ConfigureAwait(false);
            ThrowError("get_1", remotename);
        }
        #endregion

        #region IBackend implementation
        public IEnumerable<IFileEntry> List()
        {
            return m_backend.List();
        }
        public async Task PutAsync(string remotename, string filename, CancellationToken cancelToken)
        {
            ThrowError("put_0", remotename);
            await m_backend.PutAsync(remotename, filename, cancelToken).ConfigureAwait(false);
            ThrowError("put_1", remotename);
        }
        public async Task GetAsync(string remotename, string filename, CancellationToken cancelToken)
        {
            ThrowError("get_0", remotename);
            await m_backend.GetAsync(remotename, filename, cancelToken).ConfigureAwait(false);
            ThrowError("get_1", remotename);
        }
        public async Task DeleteAsync(string remotename, CancellationToken cancelToken)
        {
            ThrowError("delete_0", remotename);
            await m_backend.DeleteAsync(remotename, cancelToken).ConfigureAwait(false);
            ThrowError("delete_1", remotename);
        }
        public Task TestAsync(CancellationToken cancelToken)
        {
            return m_backend.TestAsync(cancelToken);
        }
        public Task CreateFolderAsync(CancellationToken cancelToken)
        {
            return m_backend.CreateFolderAsync(cancelToken);
        }
        public Task<string[]> GetDNSNamesAsync(CancellationToken cancelToken)
        {
            return m_backend.GetDNSNamesAsync(cancelToken);
        }
        public string DisplayName
        {
            get
            {
                return "Deterministic Error Backend";
            }
        }
        public string ProtocolKey
        {
            get
            {
                return "deterror";
            }
        }
        public IList<ICommandLineArgument> SupportedCommands
        {
            get
            {
                if (m_backend == null)
                    try { return Duplicati.Library.DynamicLoader.BackendLoader.GetSupportedCommands(WrappedBackend + "://").ToList(); }
                    catch { }

                return m_backend.SupportedCommands;
            }
        }
        public string Description
        {
            get
            {
                return "A testing backend that randomly fails";
            }
        }
        #endregion
        #region IDisposable implementation
        public void Dispose()
        {
            if (m_backend != null)
                try { m_backend.Dispose(); }
                finally { m_backend = null; }
        }
        #endregion
    }
}

