using System;

namespace Wizard.Client.Duel
{
    // 한 기기 2인(spec 10 19의 4, ARCHITECTURE 6.4): 키보드 하나를 키 배치 둘로 나눠 사람마다 입력원 하나를 낸다.
    // 유니티 쪽 키보드 입력이 물리 키 변화를 하드웨어 시각과 함께 OnPhysicalKey 로 넘기면, 두 사람의 LogicalKeyTracker 가 각자의 배치로 읽는다.
    // 두 배치가 같은 물리 키를 쓰면 두 사람 모두에게 간다(겹침 검사는 설정 쪽 M9-SET-01 이 한다). 처음 배치는 부르는 쪽이 준다
    // (예: 사람 0 = KeyBindings.Default, 사람 1 = KeyBindings.Alternate. 두 배치는 일시정지 Esc 말고는 겹치지 않고, 결투에는 일시정지가 없다).
    public sealed class SplitKeyboardInput
    {
        private readonly SplitKeyboardPlayer[] _players;

        public SplitKeyboardInput(KeyBindings player0, KeyBindings player1)
        {
            if (player0 == null)
            {
                throw new ArgumentNullException(nameof(player0));
            }
            if (player1 == null)
            {
                throw new ArgumentNullException(nameof(player1));
            }
            _players = new[] { new SplitKeyboardPlayer(player0), new SplitKeyboardPlayer(player1) };
        }

        public void OnPhysicalKey(PhysicalKey key, bool down, double timeSeconds)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                _players[i].OnPhysicalKey(key, down, timeSeconds);
            }
        }

        // index = 결투 슬롯(0, 1)
        public IInputSource Player(int index)
        {
            if (index < 0 || index >= _players.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _players[index];
        }
    }
}
