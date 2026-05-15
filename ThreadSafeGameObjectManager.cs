using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GameObjectHelper.ThreadSafeDalamudObjectTable
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

        public IObjectTable DalamudObjectTable => _objectTable;

        public static ConcurrentDictionary<nint, ThreadSafeGameObject> SafeGameObjectDictionary { get => _safeGameObjectDictionary; set => _safeGameObjectDictionary = value; }

        public nint Address => _address;

        public int Length => _length;

        public int UpdateRate { get => _updateRate; set => _updateRate = value; }
        public bool PauseTrackingForNonLocalPlayerObjects { get => _pauseTrackingForNonLocalPlayerObjects; set => _pauseTrackingForNonLocalPlayerObjects = value; }
        public bool OnlyTrackCharacterObjects { get => _onlyTrackCharacterObjects; set => _onlyTrackCharacterObjects = value; }
        public bool DoProfiling { get => _doProfiling; set => _doProfiling = value; }

        public IEnumerable<IBattleChara> PlayerObjects => _objectTable.PlayerObjects;

        public IEnumerable<IGameObject> CharacterManagerObjects => _objectTable.CharacterManagerObjects;

        public IEnumerable<IGameObject> ClientObjects => _objectTable.ClientObjects;

        public IEnumerable<IGameObject> EventObjects => _objectTable.EventObjects;

        public IEnumerable<IGameObject> StandObjects => _objectTable.StandObjects;

        public IEnumerable<IGameObject> ReactionEventObjects => _objectTable.ReactionEventObjects;

        IPlayerCharacter? IObjectTable.LocalPlayer => LocalPlayer as IPlayerCharacter;

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
        private volatile bool _loggedOut;

        private Stopwatch _loginGraceTimer = new Stopwatch();

        public ThreadSafeGameObjectManager(IClientState clientState, IObjectTable objectTable, IFramework framework, IPluginLog pluginLog)
        {
            _clientState = clientState;
            _objectTable = objectTable;
            _framework = framework;
            _pluginLog = pluginLog;
            _framework.Update += _framework_Update;
            _clientState.TerritoryChanged += _clientState_TerritoryChanged;
            _clientState.Logout += _clientState_Logout;
            _clientState.Login += _clientState_Login;
            _rateLimitTimer.Start();
            _performanceTimer = new Stopwatch();
            _parent = this;
        }

        private void _clientState_Logout(int type, int code)
        {
            _loggedOut = true;
            _localPlayer = null;
            _safeGameObjectDictionary.Clear();
            _safeGameObjectByIndex.Clear();
            _safeGameObjectByEntityId.Clear();
            _safeGameObjectByGameObjectId.Clear();
        }

        private void _clientState_Login()
        {
            _loggedOut = false;
            _loginGraceTimer.Restart();
        }

        private void _clientState_TerritoryChanged(uint obj)
        {
            _localPlayer = null;
            _safeGameObjectDictionary.Clear();
            _safeGameObjectByIndex.Clear();
            _safeGameObjectByEntityId.Clear();
            _safeGameObjectByGameObjectId.Clear();
        }

        private void _framework_Update(IFramework framework)
        {
            if (_doProfiling)
            {
                _performanceTimer.Restart();
            }
            if (framework.IsInFrameworkUpdateThread && _clientState.IsLoggedIn && !_loggedOut)
            {
                if (_loginGraceTimer.IsRunning && _loginGraceTimer.ElapsedMilliseconds < 3000)
                {
                    return; // Grace period to avoid partially constructed memory on login
                }

                if (_rateLimitTimer.ElapsedMilliseconds > _updateRate)
                {
                    // Validate that native object memory is still accessible.
                    // LocalPlayer access dereferences native pointers — if the game
                    // is between logout and the IsLoggedIn flag flip, this will AV.
                    IPlayerCharacter nativeLocalPlayer = null;
                    try
                    {
                        nativeLocalPlayer = _objectTable.LocalPlayer;
                    }
                    catch
                    {
                        // Native memory gone — bail out
                        _localPlayer = null;
                        _rateLimitTimer.Restart();
                        return;
                    }

                    _address = _objectTable.Address;
                    _length = _objectTable.Length;
                    if (nativeLocalPlayer == null || !nativeLocalPlayer.IsValid())
                    {
                        _localPlayer = null;
                    }
                    else if (_localPlayer == null)
                    {
                        _localPlayer = new ThreadSafePlayerCharacter(this, framework, nativeLocalPlayer);
                    }
                    else
                    {
                        _localPlayer.UpdateData(this, nativeLocalPlayer);
                    }
                    if (!_pauseTrackingForNonLocalPlayerObjects)
                    {
                        try
                        {
                            foreach (var gameObject in _objectTable)
                            {
                                try
                                {
                                    if (gameObject != null && gameObject.IsValid())
                                    {
                                        if (!_onlyTrackCharacterObjects || gameObject is ICharacter)
                                        {
                                            RefreshByManualProperties(gameObject);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _pluginLog.Warning(ex, ex.Message);
                                }
                            }
                        }
                        catch
                        {
                            // Object table enumeration failed — native memory freed mid-iteration
                        }
                    }
                    foreach (var kvp in _safeGameObjectDictionary)
                    {
                        if (!kvp.Value.IsValid())
                        {
                            try
                            {
                                if (_safeGameObjectDictionary.TryRemove(kvp.Key, out var threadSafeGameObject) && threadSafeGameObject != null)
                                {
                                    _safeGameObjectByIndex.TryRemove(threadSafeGameObject.ObjectIndex, out _);
                                    _safeGameObjectByEntityId.TryRemove(threadSafeGameObject.EntityId, out _);
                                    _safeGameObjectByGameObjectId.TryRemove(threadSafeGameObject.GameObjectId, out _);
                                }
                            }
                            catch
                            {

                            }
                        }
                    }
                    _rateLimitTimer.Restart();
                }
            }
            if (_doProfiling)
            {
                _pluginLog.Verbose("Object Table copy took " + _performanceTimer.ElapsedMilliseconds + "ms");
            }
        }
        public static ThreadSafeGameObject GetThreadSafeGameObject(IGameObject gameObject, bool isTarget)
        {
            return ThreadSafeGameObjectManager.SafeGameObjectDictionary.GetOrAdd(gameObject.Address, _ =>
            {
                if (gameObject is IPlayerCharacter) {
                    return new ThreadSafePlayerCharacter(_parent, _framework, gameObject, isTarget);
                } else if (gameObject is ICharacter) {
                    return new ThreadSafeCharacter(_parent, _framework, gameObject, isTarget);
                } else {
                    return new ThreadSafeGameObject(_parent, _framework, gameObject, isTarget);
                }
            });
        }

        private void RefreshByManualProperties(IGameObject gameObject)
        {
            var value = _safeGameObjectDictionary.GetOrAdd(gameObject.Address, _ =>
            {
                ThreadSafeGameObject newObj;
                if (gameObject is IPlayerCharacter) {
                    newObj = new ThreadSafePlayerCharacter(this, _framework, gameObject);
                } else if (gameObject is ICharacter) {
                    newObj = new ThreadSafeCharacter(this, _framework, gameObject);
                } else {
                    newObj = new ThreadSafeGameObject(this, _framework, gameObject);
                }
                
                _safeGameObjectByEntityId[gameObject.EntityId] = newObj;
                _safeGameObjectByGameObjectId[gameObject.GameObjectId] = newObj;
                _safeGameObjectByIndex[gameObject.ObjectIndex] = newObj;
                return newObj;
            });

            value.UpdateData(this, gameObject);
            _safeGameObjectByEntityId[gameObject.EntityId] = value;
            _safeGameObjectByGameObjectId[gameObject.GameObjectId] = value;
            _safeGameObjectByIndex[gameObject.ObjectIndex] = value;
        }

        public IGameObject? SearchById(ulong gameObjectId)
        {
            if (_safeGameObjectByGameObjectId.ContainsKey(gameObjectId))
            {
                return _safeGameObjectByGameObjectId[gameObjectId];
            }
            else
            {
                return null;
            }
        }

        public IGameObject? SearchByEntityId(uint entityId)
        {
            if (_safeGameObjectByEntityId.ContainsKey(entityId))
            {
                return _safeGameObjectByEntityId[entityId];
            }
            else
            {
                return null;
            }
        }

        public nint GetObjectAddress(int index)
        {
            if (_safeGameObjectByIndex.ContainsKey(index))
            {
                return _safeGameObjectByIndex[index].Address;
            }
            else
            {
                return 0;
            }
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
            _loggedOut = true;
            _framework.Update -= _framework_Update;
            _clientState.TerritoryChanged -= _clientState_TerritoryChanged;
            _clientState.Logout -= _clientState_Logout;
            _clientState.Login -= _clientState_Login;
        }
    }
}
