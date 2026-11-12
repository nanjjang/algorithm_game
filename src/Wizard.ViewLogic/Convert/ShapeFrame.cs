using System;

namespace Wizard.ViewLogic.Convert
{
    // 한 틱에 본 도형들. 플레이어는 슬롯 순, 나머지 개체는 원천이 준 순서다. 크기는 고정이고 넘치면 버린 수를 센다(표시 전용).
    public sealed class ShapeFrame
    {
        private readonly ShapeSample[] _players;
        private readonly ShapeSample[] _entities;
        private int _playerCount;
        private int _entityCount;

        public ShapeFrame(int playerCapacity, int entityCapacity)
        {
            if (playerCapacity < 0 || entityCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(playerCapacity < 0 ? nameof(playerCapacity) : nameof(entityCapacity));
            }
            _players = new ShapeSample[playerCapacity];
            _entities = new ShapeSample[entityCapacity];
        }

        // 이 묶음이 본 틱(ISimView.Tick). HasTick 이 거짓이면 아직 아무것도 보지 않았다
        public uint Tick { get; private set; }

        public bool HasTick { get; private set; }

        public int PlayerCount => _playerCount;

        public int EntityCount => _entityCount;

        public int PlayerCapacity => _players.Length;

        public int EntityCapacity => _entities.Length;

        // 자리가 모자라 버린 개체 수(이 틱)
        public int DroppedCount { get; private set; }

        public void Reset(uint tick)
        {
            Tick = tick;
            HasTick = true;
            _playerCount = 0;
            _entityCount = 0;
            DroppedCount = 0;
        }

        public void Clear()
        {
            Tick = 0;
            HasTick = false;
            _playerCount = 0;
            _entityCount = 0;
            DroppedCount = 0;
        }

        public bool AddPlayer(in ShapeSample sample)
        {
            if (_playerCount >= _players.Length)
            {
                DroppedCount++;
                return false;
            }
            _players[_playerCount++] = sample;
            return true;
        }

        public bool AddEntity(in ShapeSample sample)
        {
            if (_entityCount >= _entities.Length)
            {
                DroppedCount++;
                return false;
            }
            _entities[_entityCount++] = sample;
            return true;
        }

        public ref readonly ShapeSample Player(int index)
        {
            if ((uint)index >= (uint)_playerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return ref _players[index];
        }

        public ref readonly ShapeSample Entity(int index)
        {
            if ((uint)index >= (uint)_entityCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return ref _entities[index];
        }
    }
}
