using Dalamud.Game.ClientState.Customize;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;

namespace GameObjectHelper.ThreadSafeDalamudObjectTable {
    public class ThreadSafeGameObject : IGameObject, ICharacter, IPlayerCharacter {
        string _json = "";
        private nint _address;
        private SeString _name;
        private Vector3 _position;
        private float _rotation;
        private uint _dataId;
        private uint _entityId;
        private ulong _gameObjectId;
        private byte[] _customize;
        private ICustomizeData _customizeData;
        private RowRef<ClassJob> _classJob;
        private SeString _companyTag;
        private uint _currentCp;
        private uint _currentMp;
        private uint _currentGp;
        private uint _currentHp;
        private uint _maxHp;
        private uint _maxMp;
        private uint _maxGp;
        private uint _maxCp;
        private RowRef<Lumina.Excel.Sheets.Companion>? _currentMinion;
        private uint _nameId;
        private float _hitboxRadius;
        private bool _isDead;
        private ushort _objectIndex;
        private byte _shieldPercentage;
        private StatusFlags _statusFlags;
        private RowRef<OnlineStatus> _onlineStatus;
        private byte _level;
        private RowRef<Mount>? _currentMount;
        private byte _subKind;
        private ThreadSafeGameObject _targetObject;
        private ulong _targetObjectId;
        private byte _yalmDistanceX;
        private byte _yalmDistanceZ;
        private Vector3 _getMapCoordinates;
        private uint _ownerId;
        private bool _isTargetable;
        private ICharacter? _character;
        private ObjectKind _objectKind;
        private DateTime _lastUpdated;
        private RowRef<World> _currentWorld;
        private RowRef<World> _homeWorld;
        private StatusList _statusList;
        private bool _isCasting;
        private bool _isCastInterruptible;
        private byte _castActionType;
        private uint _castActionId;
        private ulong _castTargetObjectId;
        private float _currentCastTime;
        private float _baseCastTime;
        private float _totalCastTime;
        private IFramework _framework;
        private IGameObject _gameObject;
        private ThreadSafeGameObjectManager _instance;
        private IPlayerCharacter? _playerCharacter;
        private uint _baseId;

        internal ThreadSafeGameObject(ThreadSafeGameObjectManager parent, IFramework framework, IGameObject gameObject, bool isTarget = false) {
            _framework = framework;
            UpdateData(parent, gameObject, isTarget);
        }

        private bool UseLiveObject => _framework.IsInFrameworkUpdateThread && _gameObject != null && _gameObject.IsValid() && IsValid();

        public nint Address { get => _address; }
        public SeString Name { get => _name; }
        public Vector3 Position { get => UseLiveObject ? _gameObject.Position : _position; }
        public float Rotation { get => UseLiveObject ? _gameObject.Rotation : _rotation; }
        public uint DataId { get => _dataId; }
        public uint EntityId { get => _entityId; }
        public ulong GameObjectId { get => _gameObjectId; }
        public Span<byte> Customize { get => UseLiveObject && _character != null ? _character.Customize : _customize; }
        public ICustomizeData CustomizeData { get => UseLiveObject && _character != null ? _character.CustomizeData : _customizeData; }
        public RowRef<ClassJob> ClassJob { get => UseLiveObject && _character != null ? _character.ClassJob : _classJob; }
        public SeString CompanyTag { get => UseLiveObject && _character != null ? _character.CompanyTag : _companyTag; }
        public uint CurrentCp { get => UseLiveObject && _character != null ? _character.CurrentCp : _currentCp; }
        public uint CurrentMp { get => UseLiveObject && _character != null ? _character.CurrentMp : _currentMp; }
        public uint CurrentGp { get => UseLiveObject && _character != null ? _character.CurrentGp : _currentGp; }
        public uint CurrentHp { get => UseLiveObject && _character != null ? _character.CurrentHp : _currentHp; }
        public RowRef<Lumina.Excel.Sheets.Companion>? CurrentMinion { get => UseLiveObject && _character != null ? _character.CurrentMinion : _currentMinion; }
        public uint NameId { get => UseLiveObject && _character != null ? _character.NameId : _nameId; }
        public float HitboxRadius { get => UseLiveObject ? _gameObject.HitboxRadius : _hitboxRadius; }
        public bool IsDead { get => UseLiveObject ? _gameObject.IsDead : _isDead; }
        public ushort ObjectIndex { get => _objectIndex; }
        public byte ShieldPercentage { get => UseLiveObject && _character != null ? _character.ShieldPercentage : _shieldPercentage; }
        public StatusFlags StatusFlags { get => UseLiveObject && _character != null ? _character.StatusFlags : _statusFlags; }
        public RowRef<OnlineStatus> OnlineStatus { get => UseLiveObject && _character != null ? _character.OnlineStatus : _onlineStatus; }
        public byte SubKind { get => UseLiveObject ? _gameObject.SubKind : _subKind; }
        public ThreadSafeGameObject? TargetObject { get => _targetObject; }
        public ulong TargetObjectId { get => _framework.IsFrameworkUnloading && UseLiveObject ? _gameObject.TargetObjectId : _targetObjectId; }
        public byte YalmDistanceX { get => _framework.IsFrameworkUnloading && UseLiveObject ? _gameObject.YalmDistanceX : _yalmDistanceX; }
        public byte YalmDistanceZ { get => _framework.IsFrameworkUnloading && UseLiveObject ? _gameObject.YalmDistanceZ : _yalmDistanceZ; }
        public Vector3 GetMapCoordinates { get => UseLiveObject ? _gameObject.GetMapCoordinates() : _getMapCoordinates; }
        public byte Level { get => UseLiveObject && _character != null ? _character.Level : _level; }
        public bool IsTargetable { get => UseLiveObject ? _gameObject.IsTargetable : _isTargetable; }

        public uint OwnerId => UseLiveObject ? _gameObject.OwnerId : _ownerId;
        public ObjectKind ObjectKind => UseLiveObject ? _gameObject.ObjectKind : _objectKind;

        IGameObject? IGameObject.TargetObject => UseLiveObject ? _gameObject.TargetObject : TargetObject;

        public uint MaxHp { get => UseLiveObject && _character != null ? _character.MaxHp : _maxHp; }
        public uint MaxMp { get => UseLiveObject && _character != null ? _character.MaxMp : _maxMp; }
        public uint MaxGp { get => UseLiveObject && _character != null ? _character.MaxGp : _maxGp; }
        public uint MaxCp { get => UseLiveObject && _character != null ? _character.MaxCp : _maxCp; }
        public RowRef<Mount>? CurrentMount { get => UseLiveObject && _character != null ? _character.CurrentMount : _currentMount; }


        public RowRef<World> HomeWorld => UseLiveObject && _playerCharacter != null ? _playerCharacter.HomeWorld : _homeWorld;

        public StatusList StatusList => UseLiveObject && _playerCharacter != null ? _playerCharacter.StatusList : _statusList;

        public bool IsCasting => UseLiveObject && _playerCharacter != null ? _playerCharacter.IsCasting : _isCasting;

        public bool IsCastInterruptible => UseLiveObject && _playerCharacter != null ? _playerCharacter.IsCastInterruptible : _isCastInterruptible;

        public byte CastActionType => UseLiveObject && _playerCharacter != null ? _playerCharacter.CastActionType : _castActionType;

        public uint CastActionId => UseLiveObject && _playerCharacter != null ? _playerCharacter.CastActionId : _castActionId;

        public ulong CastTargetObjectId => UseLiveObject && _playerCharacter != null ? _playerCharacter.CastTargetObjectId : _castTargetObjectId;

        public float CurrentCastTime => UseLiveObject && _playerCharacter != null ? _playerCharacter.CurrentCastTime : _currentCastTime;

        public float BaseCastTime => UseLiveObject && _playerCharacter != null ? _playerCharacter.BaseCastTime : _baseCastTime;

        public float TotalCastTime => UseLiveObject && _playerCharacter != null ? _playerCharacter.TotalCastTime : _totalCastTime;

        public RowRef<World> CurrentWorld { get => UseLiveObject && _playerCharacter != null ? _playerCharacter.CurrentWorld : _currentWorld; }
        public ThreadSafeGameObjectManager Instance { get => _instance; set => _instance = value; }

        public uint BaseId => UseLiveObject && _playerCharacter != null ? _playerCharacter.BaseId : _baseId;

        internal void UpdateData(ThreadSafeGameObjectManager parent, IGameObject gameObject, bool isTarget = false) {
            _gameObject = gameObject;
            _instance = parent;
            if (_framework.IsInFrameworkUpdateThread && gameObject != null) {
                try {
                    _address = gameObject.Address;
                    _name = gameObject.Name.TextValue;
                    _position = gameObject.Position;
                    _rotation = gameObject.Rotation;
                    _dataId = gameObject.DataId;
                    _entityId = gameObject.EntityId;
                    _gameObjectId = gameObject.GameObjectId;
                    _isDead = gameObject.IsDead;
                    _hitboxRadius = gameObject.HitboxRadius;
                    _objectIndex = gameObject.ObjectIndex;
                    _subKind = gameObject.SubKind;
                    _targetObjectId = gameObject.TargetObjectId;
                    _yalmDistanceX = gameObject.YalmDistanceX;
                    _yalmDistanceZ = gameObject.YalmDistanceZ;
                    _getMapCoordinates = gameObject.GetMapCoordinates();
                    _ownerId = gameObject.OwnerId;
                    _objectKind = gameObject.ObjectKind;
                    _baseId = gameObject.BaseId;
                    if (!isTarget) {
                        if (gameObject.TargetObject != null) {
                            _targetObject = ThreadSafeGameObjectManager.GetThreadSafeGameObject(gameObject.TargetObject, true);
                        } else {
                            _targetObject = null;
                        }
                    }
                    _isTargetable = gameObject.IsTargetable;

                    _character = gameObject as ICharacter;
                    if (_character != null) {
                        _customize = _character.Customize.ToArray();
                        _customizeData = _character.CustomizeData;
                        _classJob = _character.ClassJob;
                        _companyTag = _character.CompanyTag;
                        _currentCp = _character.CurrentCp;
                        _currentMp = _character.CurrentMp;
                        _currentGp = _character.CurrentGp;
                        _currentHp = _character.CurrentHp;
                        _maxHp = _character.MaxHp;
                        _maxMp = _character.MaxMp;
                        _maxGp = _character.MaxGp;
                        _maxCp = _character.MaxCp;
                        _currentMinion = _character.CurrentMinion;
                        _nameId = _character.NameId;
                        _shieldPercentage = _character.ShieldPercentage;
                        _statusFlags = _character.StatusFlags;
                        _onlineStatus = _character.OnlineStatus;
                        _level = _character.Level;
                        _currentMount = _character.CurrentMount;
                    }
                    _playerCharacter = gameObject as IPlayerCharacter;
                    if (_playerCharacter != null) {
                        _currentWorld = _playerCharacter.CurrentWorld;
                        _homeWorld = _playerCharacter.HomeWorld;
                        _statusList = _playerCharacter.StatusList;
                        _isCasting = _playerCharacter.IsCasting;
                        _isCastInterruptible = _playerCharacter.IsCastInterruptible;
                        _castActionType = _playerCharacter.CastActionType;
                        _castActionId = _playerCharacter.CastActionId;
                        _castTargetObjectId = _playerCharacter.CastTargetObjectId;
                        _currentCastTime = _playerCharacter.CurrentCastTime;
                        _baseCastTime = _playerCharacter.BaseCastTime;
                        _totalCastTime = _playerCharacter.TotalCastTime;
                    }
                    _lastUpdated = DateTime.UtcNow;
                } catch { }
            }
        }

        public bool IsValid() {
            TimeSpan ts = DateTime.UtcNow - _lastUpdated;
            return ts.TotalMilliseconds < _instance.UpdateRate + 10;
        }

        public bool Equals(IGameObject? other) {
            return other.Address == Address;
        }
    }
}
