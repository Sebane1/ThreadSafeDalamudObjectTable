using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameObjectHelper.ThreadSafeDalamudObjectTable {
    public class ThreadSafeGameObjectManager : IObjectTable, IDisposable {
        static ConcurrentDictionary<nint, ThreadSafeGameObject> _safeGameObjectDictionary = new ConcurrentDictionary<nint, ThreadSafeGameObject>();
        static ConcurrentDictionary<int, ThreadSafeGameObject> _safeGameObjectByIndex = new ConcurrentDictionary<int, ThreadSafeGameObject>();
        static ConcurrentDictionary<uint, ThreadSafeGameObject> _safeGameObjectByEntityId = new ConcurrentDictionary<uint, ThreadSafeGameObject>();
        static ConcurrentDictionary<ulong, ThreadSafeGameObject> _safeGameObjectByGameObjectId = new ConcurrentDictionary<ulong, ThreadSafeGameObject>();
        public ThreadSafeGameObject LocalPlayer {
            get {
                return _localPlayer;
            }
        }

        public static ConcurrentDictionary<nint, ThreadSafeGameObject> SafeGameObjectDictionary { get => _safeGameObjectDictionary; set => _safeGameObjectDictionary = value; }

        public nint Address => _address;

        public int Length => _length;

        public int UpdateRate { get => _updateRate; set => _updateRate = value; }
        public bool PauseTrackingForNonLocalPlayerObjects { get => _pauseTrackingForNonLocalPlayerObjects; set => _pauseTrackingForNonLocalPlayerObjects = value; }
        public bool OnlyTrackCharacterObjects { get => _onlyTrackCharacterObjects; set => _onlyTrackCharacterObjects = value; }
        public bool DoProfiling { get => _doProfiling; set => _doProfiling = value; }

        public IEnumerable<IBattleChara> PlayerObjects => throw new NotImplementedException();

        public IEnumerable<IGameObject> CharacterManagerObjects => throw new NotImplementedException();

        public IEnumerable<IGameObject> ClientObjects => throw new NotImplementedException();

        public IEnumerable<IGameObject> EventObjects => throw new NotImplementedException();

        public IEnumerable<IGameObject> StandObjects => throw new NotImplementedException();

        public IEnumerable<IGameObject> ReactionEventObjects => throw new NotImplementedException();

        public IGameObject? this[int index] => _safeGameObjectByIndex[index];

        private IClientState _clientState;
        private IObjectTable _objectTable;
        private static IFramework _framework;
        private static IPluginLog _pluginLog;

        Stopwatch _rateLimitTimer = new Stopwatch();
        int _updateRate = 80;
        private ThreadSafeGameObject _localPlayer;
        private nint _address;
        private int _length;
        bool _pauseTrackingForNonLocalPlayerObjects;
        bool _onlyTrackCharacterObjects = false;
        bool _doProfiling = false;
        private Stopwatch _performanceTimer;
        private static ThreadSafeGameObjectManager _parent;

        public ThreadSafeGameObjectManager(IClientState clientState, IObjectTable objectTable, IFramework framework, IPluginLog pluginLog) {
            _clientState = clientState;
            _objectTable = objectTable;
            _framework = framework;
            _pluginLog = pluginLog;
            _framework.Update += _framework_Update;
            _clientState.TerritoryChanged += _clientState_TerritoryChanged;
            _rateLimitTimer.Start();
            _performanceTimer = new Stopwatch();
            _parent = this;
        }

        private void _clientState_TerritoryChanged(ushort obj) {
            _safeGameObjectDictionary.Clear();
            _safeGameObjectByIndex.Clear();
            _safeGameObjectByEntityId.Clear();
            _safeGameObjectByGameObjectId.Clear();
        }

        private void _framework_Update(IFramework framework) {
            if (_doProfiling) {
                _performanceTimer.Restart();
            }
            if (framework.IsInFrameworkUpdateThread && _clientState.IsLoggedIn) {
                if (_rateLimitTimer.ElapsedMilliseconds > _updateRate) {
                    _address = _objectTable.Address;
                    _length = _objectTable.Length;
                    if (_clientState.LocalPlayer == null) {
                        _localPlayer = null;
                    } else if (_localPlayer == null) {
                        _localPlayer = new ThreadSafeGameObject(this, framework, _clientState.LocalPlayer);
                    } else {
                        _localPlayer.UpdateData(this, _clientState.LocalPlayer);
                    }
                    if (!_pauseTrackingForNonLocalPlayerObjects) {
                        foreach (var gameObject in _objectTable) {
                            try {
                                if (!_onlyTrackCharacterObjects || gameObject is ICharacter) {
                                    RefreshByManualProperties(gameObject);
                                }
                            } catch (Exception ex) {
                                _pluginLog.Warning(ex, ex.Message);
                            }
                        }
                    }
                    for (int i = _safeGameObjectDictionary.Count - 1; i > 0; i--) {
                        var value = _safeGameObjectDictionary.ElementAt(i);
                        if (!value.Value.IsValid()) {
                            try {
                                _safeGameObjectDictionary.TryRemove(value.Key, out var threadSafeGameObject);
                                _safeGameObjectByIndex.TryRemove(value.Value.ObjectIndex, out threadSafeGameObject);
                                _safeGameObjectByEntityId.TryRemove(value.Value.EntityId, out threadSafeGameObject);
                                _safeGameObjectByGameObjectId.TryRemove(value.Value.GameObjectId, out threadSafeGameObject);
                            } catch {

                            }
                        }
                    }
                    _rateLimitTimer.Restart();
                }
            }
            if (_doProfiling) {
                _pluginLog.Verbose("Object Table copy took " + _performanceTimer.ElapsedMilliseconds + "ms");
            }
        }
        public static ThreadSafeGameObject GetThreadSafeGameObject(IGameObject gameObject, bool isTarget) {
            if (!ThreadSafeGameObjectManager.SafeGameObjectDictionary.ContainsKey(gameObject.Address)) {
                ThreadSafeGameObjectManager.SafeGameObjectDictionary[gameObject.Address] = new ThreadSafeGameObject(_parent, _framework, gameObject, isTarget);
            }
            return ThreadSafeGameObjectManager.SafeGameObjectDictionary[gameObject.Address];
        }

        private void RefreshByManualProperties(IGameObject gameObject) {
            ThreadSafeGameObject value = null;
            if (!_safeGameObjectDictionary.ContainsKey(gameObject.Address)) {
                _safeGameObjectDictionary[gameObject.Address] = new ThreadSafeGameObject(this, _framework, gameObject);
                value = _safeGameObjectDictionary[gameObject.Address];
                _safeGameObjectByEntityId[gameObject.EntityId] = value;
                _safeGameObjectByGameObjectId[gameObject.GameObjectId] = value;
                _safeGameObjectByIndex[gameObject.ObjectIndex] = value;
            } else {
                value = _safeGameObjectDictionary[gameObject.Address];
                value.UpdateData(this, gameObject);
            }
        }

        public IGameObject? SearchById(ulong gameObjectId) {
            if (_safeGameObjectByGameObjectId.ContainsKey(gameObjectId)) {
                return _safeGameObjectByGameObjectId[gameObjectId];
            } else {
                return null;
            }
        }

        public IGameObject? SearchByEntityId(uint entityId) {
            if (_safeGameObjectByEntityId.ContainsKey(entityId)) {
                return _safeGameObjectByEntityId[entityId];
            } else {
                return null;
            }
        }

        public nint GetObjectAddress(int index) {
            if (_safeGameObjectByIndex.ContainsKey(index)) {
                return _safeGameObjectByIndex[index].Address;
            } else {
                return 0;
            }
        }

        public IGameObject? CreateObjectReference(nint address) {
            return _objectTable.CreateObjectReference(address);
        }

        public IEnumerator<IGameObject> GetEnumerator() {
            return SafeGameObjectDictionary.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Dispose() {
            _framework.Update -= _framework_Update;
        }
    }
}
