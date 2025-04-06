using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;

namespace DragAndDropTexturing.ThreadSafeDalamudObjectTable
{
    public class ThreadSafeGameObject : IGameObject, ICharacter, IPlayerCharacter
    {
        string _json = "";
        private nint _address;
        private SeString _name;
        private Vector3 _position;
        private float _rotation;
        private uint _dataId;
        private uint _entityId;
        private ulong _gameObjectId;
        private byte[] _customize;
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

        public ThreadSafeGameObject(IGameObject gameObject, bool isTarget = false)
        {
            UpdateData(gameObject, isTarget);
        }
        public ThreadSafeGameObject()
        {

        }

        public nint Address { get => _address; }
        public SeString Name { get => _name; }
        public Vector3 Position { get => _position; }
        public float Rotation { get => _rotation; }
        public uint DataId { get => _dataId; }
        public uint EntityId { get => _entityId; }
        public ulong GameObjectId { get => _gameObjectId; }
        public byte[] Customize { get => _customize; }
        public RowRef<ClassJob> ClassJob { get => _classJob; }
        public SeString CompanyTag { get => _companyTag; }
        public uint CurrentCp { get => _currentCp; }
        public uint CurrentMp { get => _currentMp; }
        public uint CurrentGp { get => _currentGp; }
        public uint CurrentHp { get => _currentHp; }
        public RowRef<Lumina.Excel.Sheets.Companion>? CurrentMinion { get => _currentMinion; }
        public uint NameId { get => _nameId; }
        public float HitboxRadius { get => _hitboxRadius; }
        public bool IsDead { get => _isDead; }
        public ushort ObjectIndex { get => _objectIndex; }
        public byte ShieldPercentage { get => _shieldPercentage; }
        public StatusFlags StatusFlags { get => _statusFlags; }
        public RowRef<OnlineStatus> OnlineStatus { get => _onlineStatus; }
        public byte SubKind { get => _subKind; }
        public ThreadSafeGameObject? TargetObject { get => _targetObject; }
        public ulong TargetObjectId { get => _targetObjectId; }
        public byte YalmDistanceX { get => _yalmDistanceX; }
        public byte YalmDistanceZ { get => _yalmDistanceZ; }
        public Vector3 GetMapCoordinates { get => _getMapCoordinates; }
        public byte Level { get => _level; }
        public bool IsTargetable { get => _isTargetable; }

        public uint OwnerId => _ownerId;
        public ObjectKind ObjectKind => _objectKind;

        IGameObject? IGameObject.TargetObject => TargetObject;

        public uint MaxHp { get => _maxHp; set => _maxHp = value; }
        public uint MaxMp { get => _maxMp; set => _maxMp = value; }
        public uint MaxGp { get => _maxGp; set => _maxGp = value; }
        public uint MaxCp { get => _maxCp; set => _maxCp = value; }
        public RowRef<Mount>? CurrentMount { get => _currentMount; set => _currentMount = value; }


        public RowRef<World> HomeWorld => _homeWorld;

        public StatusList StatusList => _statusList;

        public bool IsCasting => _isCasting;

        public bool IsCastInterruptible => _isCastInterruptible;

        public byte CastActionType => _castActionType;

        public uint CastActionId => _castActionId;

        public ulong CastTargetObjectId => _castTargetObjectId;

        public float CurrentCastTime => _currentCastTime;

        public float BaseCastTime => _baseCastTime;

        public float TotalCastTime => _totalCastTime;

        public RowRef<World> CurrentWorld { get => _currentWorld; set => _currentWorld = value; }

        public void UpdateData(IGameObject gameObject, bool isTarget = false)
        {
            if (gameObject != null)
            {
                try
                {
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
                    if (!isTarget)
                    {
                        if (gameObject.TargetObject != null)
                        {
                            _targetObject = ThreadSafeGameObjectManager.GetThreadsafeGameObject(gameObject.TargetObject);
                        }
                        else
                        {
                            _targetObject = null;
                        }
                    }
                    _isTargetable = gameObject.IsTargetable;

                    ICharacter character = gameObject as ICharacter;
                    if (character != null)
                    {
                        _customize = character.Customize;
                        _classJob = character.ClassJob;
                        _companyTag = character.CompanyTag;
                        _currentCp = character.CurrentCp;
                        _currentMp = character.CurrentMp;
                        _currentGp = character.CurrentGp;
                        _currentHp = character.CurrentHp;
                        _maxHp = character.MaxHp;
                        _maxMp = character.MaxMp;
                        _maxGp = character.MaxGp;
                        _maxCp = character.MaxCp;
                        _currentMinion = character.CurrentMinion;
                        _nameId = character.NameId;
                        _shieldPercentage = character.ShieldPercentage;
                        _statusFlags = character.StatusFlags;
                        _onlineStatus = character.OnlineStatus;
                        _level = character.Level;
                        _currentMount = character.CurrentMount;
                    }
                    IPlayerCharacter playerCharacter = gameObject as IPlayerCharacter;
                    if (playerCharacter != null)
                    {
                        _currentWorld = playerCharacter.CurrentWorld;
                        _homeWorld = playerCharacter.HomeWorld;
                        _statusList = playerCharacter.StatusList;
                        _isCasting = playerCharacter.IsCasting;
                        _isCastInterruptible = playerCharacter.IsCastInterruptible;
                        _castActionType = playerCharacter.CastActionType;
                        _castActionId = playerCharacter.CastActionId;
                        _castTargetObjectId = playerCharacter.CastTargetObjectId;
                        _currentCastTime = playerCharacter.CurrentCastTime;
                        _baseCastTime = playerCharacter.BaseCastTime;
                        _totalCastTime = playerCharacter.TotalCastTime;
                    }
                    _lastUpdated = DateTime.Now;
                }
                catch { }
            }
        }

        public bool IsValid()
        {
            TimeSpan ts = DateTime.Now - _lastUpdated;
            return ts.TotalSeconds < 10;
        }

        public bool Equals(IGameObject? other)
        {
            return other.Address == Address;
        }
    }
}
