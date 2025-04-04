using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace DragAndDropTexturing.ThreadSafeDalamudObjectTable
{
    public class ThreadSafeGameObjectManager
    {
        static ConcurrentDictionary<nint, ThreadSafeGameObject> _safeGameObjectTable = new ConcurrentDictionary<nint, ThreadSafeGameObject>();
        public ThreadSafeGameObject LocalPlayer
        {
            get
            {
                return _localPlayer;
            }
        }

        public static ConcurrentDictionary<nint, ThreadSafeGameObject> SafeGameObjectTable { get => _safeGameObjectTable; set => _safeGameObjectTable = value; }

        private IClientState _clientState;
        private IObjectTable _objectTable;
        private IFramework _framework;
        private IPluginLog _pluginLog;

        Stopwatch _rateLimitTimer = new Stopwatch();
        int _updateRate = 10;
        private ThreadSafeGameObject _localPlayer;

        public ThreadSafeGameObjectManager(IClientState clientState, IObjectTable objectTable, IFramework framework, IPluginLog pluginLog)
        {
            _clientState = clientState;
            _objectTable = objectTable;
            _framework = framework;
            _pluginLog = pluginLog;
            _framework.Update += _framework_Update;
            _rateLimitTimer.Start();
        }

        private void _framework_Update(IFramework framework)
        {
            if (framework.IsInFrameworkUpdateThread && _clientState.IsLoggedIn)
            {
                if (_rateLimitTimer.ElapsedMilliseconds > _updateRate)
                {
                    if (_localPlayer == null)
                    {
                        _localPlayer = new ThreadSafeGameObject(_clientState.LocalPlayer);
                    }
                    else
                    {
                        _localPlayer.UpdateData(_localPlayer);
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
            if (!ThreadSafeGameObjectManager.SafeGameObjectTable.ContainsKey(gameObject.Address))
            {
                ThreadSafeGameObjectManager.SafeGameObjectTable[gameObject.Address] = new ThreadSafeGameObject(gameObject);
            }
            return ThreadSafeGameObjectManager.SafeGameObjectTable[gameObject.Address];
        }

        private void RefreshByManualProperties(IGameObject gameObject)
        {
            if (!_safeGameObjectTable.ContainsKey(gameObject.Address))
            {
                _safeGameObjectTable[gameObject.Address] = new ThreadSafeGameObject(gameObject);
            }
            else
            {
                _safeGameObjectTable[gameObject.Address].UpdateData(gameObject);
            }
        }
    }
}
