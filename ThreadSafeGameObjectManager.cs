using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace DragAndDropTexturing.ThreadSafeDalamudObjectTable
{
    public class ThreadSafeGameObjectManager : IObjectTable, IDisposable
    {
        static ConcurrentDictionary<nint, ThreadSafeGameObject> _safeGameObjectDictionary = new ConcurrentDictionary<nint, ThreadSafeGameObject>();
        static ConcurrentDictionary<int, ThreadSafeGameObject> _safeGameObjectByIndex = new ConcurrentDictionary<int, ThreadSafeGameObject>();
        static ConcurrentDictionary<uint, ThreadSafeGameObject> _safeGameObjectByEntityId = new ConcurrentDictionary<uint, ThreadSafeGameObject>();
        static ConcurrentDictionary<ulong, ThreadSafeGameObject> _safeGameObjectByGameObjectId = new ConcurrentDictionary<ulong, ThreadSafeGameObject>();
        public ThreadSafeGameObject LocalPlayer
        {
            get
            {
                return _localPlayer;
            }
        }

        public static ConcurrentDictionary<nint, ThreadSafeGameObject> SafeGameObjectDictionary { get => _safeGameObjectDictionary; set => _safeGameObjectDictionary = value; }

        public nint Address => _address;

        public int Length => _length;
        public IGameObject? this[int index] => _safeGameObjectByIndex[index];

        private IClientState _clientState;
        private IObjectTable _objectTable;
        private IFramework _framework;
        private IPluginLog _pluginLog;

        Stopwatch _rateLimitTimer = new Stopwatch();
        int _updateRate = 200;
        private ThreadSafeGameObject _localPlayer;
        private nint _address;
        private int _length;

        public ThreadSafeGameObjectManager(IClientState clientState, IObjectTable objectTable, IFramework framework, IPluginLog pluginLog)
        {
            _clientState = clientState;
            _objectTable = objectTable;
            _framework = framework;
            _pluginLog = pluginLog;
            _framework.Update += _framework_Update;
            _clientState.TerritoryChanged += _clientState_TerritoryChanged;
            _rateLimitTimer.Start();
        }

        private void _clientState_TerritoryChanged(ushort obj)
        {
            _safeGameObjectDictionary.Clear();
            _safeGameObjectByIndex.Clear();
            _safeGameObjectByEntityId.Clear();
            _safeGameObjectByGameObjectId.Clear();
        }

        private void _framework_Update(IFramework framework)
        {
            if (framework.IsInFrameworkUpdateThread && _clientState.IsLoggedIn)
            {
                if (_rateLimitTimer.ElapsedMilliseconds > _updateRate)
                {
                    _address = _objectTable.Address;
                    _length = _objectTable.Length;
                    if (_clientState.LocalPlayer == null)
                    {
                        _localPlayer = null;
                    }
                    else if (_localPlayer == null)
                    {
                        _localPlayer = new ThreadSafeGameObject(_clientState.LocalPlayer);
                    }
                    else
                    {
                        _localPlayer.UpdateData(_clientState.LocalPlayer);
                    }
                    foreach (var gameObject in _objectTable)
                    {
                        try
                        {
                            RefreshByManualProperties(gameObject);
                        }
                        catch (Exception ex)
                        {
                            _pluginLog.Warning(ex, ex.Message);
                        }
                    }
                    _rateLimitTimer.Restart();
                }
            }
        }
        public static ThreadSafeGameObject GetThreadsafeGameObject(IGameObject gameObject)
        {
            if (!ThreadSafeGameObjectManager.SafeGameObjectDictionary.ContainsKey(gameObject.Address))
            {
                ThreadSafeGameObjectManager.SafeGameObjectDictionary[gameObject.Address] = new ThreadSafeGameObject(gameObject);
            }
            return ThreadSafeGameObjectManager.SafeGameObjectDictionary[gameObject.Address];
        }

        private void RefreshByManualProperties(IGameObject gameObject)
        {
            ThreadSafeGameObject value = null;
            if (!_safeGameObjectDictionary.ContainsKey(gameObject.Address))
            {
                _safeGameObjectDictionary[gameObject.Address] = new ThreadSafeGameObject(gameObject);
                value = _safeGameObjectDictionary[gameObject.Address];
            }
            else
            {
                value = _safeGameObjectDictionary[gameObject.Address];
                value.UpdateData(gameObject);
            }
            _safeGameObjectByEntityId[gameObject.EntityId] = value;
            _safeGameObjectByGameObjectId[gameObject.GameObjectId] = value;
            _safeGameObjectByIndex[gameObject.ObjectIndex] = value;
        }

        public IGameObject? SearchById(ulong gameObjectId)
        {
            return _safeGameObjectByGameObjectId[gameObjectId];
        }

        public IGameObject? SearchByEntityId(uint entityId)
        {
            return _safeGameObjectByEntityId[entityId];
        }

        public nint GetObjectAddress(int index)
        {
            return _safeGameObjectByIndex[index].Address;
        }

        public IGameObject? CreateObjectReference(nint address)
        {
            return _objectTable.CreateObjectReference(address);
        }

        public IEnumerator<IGameObject> GetEnumerator()
        {
            return SafeGameObjectDictionary.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            _framework.Update -= _framework_Update;
        }
    }
}
